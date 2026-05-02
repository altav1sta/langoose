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

            await MarkCompletedAsync(jobId, state, stoppingToken);

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

            await MarkFailedAsync(jobId, failed, CancellationToken.None);

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
