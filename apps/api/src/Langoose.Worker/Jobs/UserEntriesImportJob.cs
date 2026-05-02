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

            await MarkCompletedAsync(jobId, state, ct);

            return state.TotalCount > 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await MarkFailedAsync(jobId, new() { ErrorMessage = "Cancelled" }, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "User-entries import job {JobId} failed.", jobId);
            await MarkFailedAsync(jobId, new() { ErrorMessage = ex.Message }, CancellationToken.None);
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

    private async Task MarkCompletedAsync(Guid jobId, BulkJobState state, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);
        var now = DateTimeOffset.UtcNow;

        job.Status = JobStatus.Completed;
        job.FinishedAtUtc = now;
        job.UpdatedAtUtc = now;
        job.ExecutionState = JsonSerializer.Serialize(state, AppJsonOptions.Default);

        await db.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAsync(Guid jobId, BulkJobState state, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);
        var now = DateTimeOffset.UtcNow;

        job.Status = JobStatus.Failed;
        job.FinishedAtUtc = now;
        job.UpdatedAtUtc = now;
        job.ExecutionState = JsonSerializer.Serialize(state, AppJsonOptions.Default);

        await db.SaveChangesAsync(ct);
    }
}
