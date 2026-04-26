using Langoose.Core.Configuration;
using Langoose.Data;
using Langoose.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Langoose.Worker.Jobs;

/// <summary>
/// Polls <c>background_jobs</c> for <c>BulkImport</c> work. Claims
/// <c>Pending</c> rows (oldest first by Id, which is time-ordered v7),
/// marks them <c>Running</c>, dispatches to <see cref="BulkImportJobHandler"/>,
/// and sleeps for the configured poll interval. One service instance
/// per job type — future <c>AiValidation</c> and <c>Promotion</c>
/// services live alongside this one and run in parallel.
///
/// Lifecycle is forward-only: Pending → Running → (Completed | Failed |
/// Cancelled), all three terminal. A worker crash mid-job leaves the row
/// in <c>Running</c> with no automatic recovery — operator intervention
/// (DB update or re-submitting a fresh job) is required.
/// </summary>
public sealed class BulkImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<BackgroundJobsSettings> options,
    ILogger<BulkImportBackgroundService> logger) : BackgroundService
{
    private const JobType ServiceJobType = JobType.BulkImport;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BulkImportBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await ClaimNextJobAsync(stoppingToken);

                if (jobId is null)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                logger.LogInformation("Dispatching bulk-import job {JobId}.", jobId);

                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<BulkImportJobHandler>();

                await handler.RunAsync(jobId.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in bulk-import poll cycle.");
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        logger.LogInformation("BulkImportBackgroundService is stopping.");
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
}
