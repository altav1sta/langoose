using System.Text.Json;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Jobs;
using Langoose.Domain.Services;
using Langoose.Worker.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace Langoose.Worker.Jobs;

public sealed class UserEntriesImportJob(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<UserEntriesImportSettings> settings,
    IVariantFeatureManager featureManager,
    ILogger<UserEntriesImportJob> logger) : BackgroundService
{
    private const string FeatureFlag = "EnableUserEntriesImport";

    private readonly UserEntriesImportSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("UserEntriesImportJob is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunOnceAsync(stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in user-entries import poll cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.PollIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("UserEntriesImportJob is stopping.");
    }

    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        if (!await featureManager.IsEnabledAsync(FeatureFlag, ct))
        {
            logger.LogDebug("Feature flag {Flag} is disabled, skipping poll.", FeatureFlag);
            return false;
        }

        var jobId = await CreateRunningJobAsync(_settings.BatchSize, _settings.MaxRetries, ct);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserEntriesImportService>();

            var state = await service.RunBatchAsync(_settings.BatchSize, _settings.MaxRetries, ct);

            if (!await TryMarkCompletedAsync(jobId, state, ct))
            {
                logger.LogInformation("User-entries import job {JobId} cancelled by operator.", jobId);
                return false;
            }

            return state.TotalCount > 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await TryMarkFailedAsync(jobId, new() { ErrorMessage = "Cancelled" }, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "User-entries import job {JobId} failed.", jobId);
            await TryMarkFailedAsync(jobId, new() { ErrorMessage = ex.Message }, CancellationToken.None);
            return false;
        }
    }

    private async Task<Guid> CreateRunningJobAsync(int batchSize, int maxRetries, CancellationToken ct)
    {
        var jobId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        db.BackgroundJobs.Add(new()
        {
            Id = jobId,
            Type = JobType.UserEntriesImport,
            Status = JobStatus.Running,
            Settings = JsonSerializer.Serialize(
                new UserEntriesImportParams(batchSize, maxRetries), AppJsonOptions.Default),
            ExecutionState = null,
            CreatedAtUtc = now,
            StartedAtUtc = now,
            UpdatedAtUtc = now
        });

        await db.SaveChangesAsync(ct);

        return jobId;
    }

    private Task<bool> TryMarkCompletedAsync(Guid jobId, BulkJobState state, CancellationToken ct) =>
        TryTransitionAsync(jobId, state, JobStatus.Completed, ct);

    private Task<bool> TryMarkFailedAsync(Guid jobId, BulkJobState state, CancellationToken ct) =>
        TryTransitionAsync(jobId, state, JobStatus.Failed, ct);

    private async Task<bool> TryTransitionAsync(
        Guid jobId, BulkJobState state, JobStatus terminalStatus, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var stateJson = JsonSerializer.Serialize(state, AppJsonOptions.Default);

        var transitioned = await db.BackgroundJobs
            .Where(x => x.Id == jobId && x.Status == JobStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, terminalStatus)
                .SetProperty(x => x.FinishedAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now)
                .SetProperty(x => x.ExecutionState, stateJson), ct);

        if (transitioned > 0)
            return true;

        await db.BackgroundJobs
            .Where(x => x.Id == jobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.UpdatedAtUtc, now)
                .SetProperty(x => x.ExecutionState, stateJson), ct);

        return false;
    }
}
