using System.Text.Json;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Jobs;
using Langoose.Domain.Services;
using Langoose.Worker.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Langoose.Worker.Jobs;

public sealed class CorpusImportJob(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<CorpusImportSettings> options,
    ILogger<CorpusImportJob> logger) : BackgroundService
{
    private const JobType ServiceJobType = JobType.CorpusImport;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    private readonly int _batchSize = options.Value.BatchSize;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CorpusImportJob is starting.");

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
                logger.LogError(ex, "Unhandled error in corpus-import poll cycle.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        logger.LogInformation("CorpusImportJob is stopping.");
    }

    public async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        var jobId = await ClaimNextJobAsync(stoppingToken);

        if (jobId is null)
            return false;

        logger.LogInformation("Dispatching corpus-import job {JobId}.", jobId);

        var state = await DispatchAsync(jobId.Value, stoppingToken);

        return state.TotalCount > 0;
    }

    private async Task<BulkJobState> DispatchAsync(Guid jobId, CancellationToken stoppingToken)
    {
        var settings = await LoadSettingsAsync(jobId, stoppingToken);

        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICorpusImportService>();

        try
        {
            var state = await service.RunBatchAsync(settings, _batchSize, stoppingToken);

            if (!await TryMarkCompletedAsync(jobId, state, stoppingToken))
            {
                logger.LogInformation("Corpus import job {JobId} cancelled by operator; chain stops.", jobId);
                return state;
            }

            if (state.Cursor is not null)
                await SubmitContinuationAsync(settings, state.Cursor, stoppingToken);

            return state;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Corpus import job {JobId} failed.", jobId);

            var failed = new BulkJobState { ErrorMessage = ex.Message };

            await TryMarkFailedAsync(jobId, failed, CancellationToken.None);

            return failed;
        }
    }

    private async Task<CorpusImportParams> LoadSettingsAsync(Guid jobId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.BackgroundJobs.AsNoTracking().FirstAsync(x => x.Id == jobId, ct);

        return JsonSerializer.Deserialize<CorpusImportParams>(job.Settings, AppJsonOptions.Default)
            ?? throw new InvalidOperationException($"Job {jobId} has empty CorpusImport params.");
    }

    private async Task<Guid?> ClaimNextJobAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var pendingId = await db.BackgroundJobs
            .Where(x => x.Type == ServiceJobType && x.Status == JobStatus.Pending)
            .OrderBy(x => x.Id)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (pendingId is null)
            return null;

        var now = DateTimeOffset.UtcNow;

        var rowsAffected = await db.BackgroundJobs
            .Where(x => x.Id == pendingId && x.Status == JobStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, JobStatus.Running)
                .SetProperty(x => x.StartedAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        return rowsAffected > 0 ? pendingId : null;
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

    private async Task SubmitContinuationAsync(CorpusImportParams previous, string nextCursor, CancellationToken ct)
    {
        var continuation = previous with { StartCursor = nextCursor };
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        db.BackgroundJobs.Add(new()
        {
            Id = Guid.CreateVersion7(),
            Type = JobType.CorpusImport,
            Status = JobStatus.Pending,
            Settings = JsonSerializer.Serialize(continuation, AppJsonOptions.Default),
            ExecutionState = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await db.SaveChangesAsync(ct);
    }
}
