using System.Text.Json;
using Langoose.Core.BulkImport;
using Langoose.Core.Configuration;
using Langoose.Corpus.Data.Readers;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Jobs;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Langoose.Worker.Jobs;

/// <summary>
/// Executes one <see cref="JobType.BulkImport"/> job end to end:
/// streams ranked Wiktionary bundles from the corpus, applies the
/// heuristic filter, and writes <see cref="ImportEntry"/> rows. Cursor
/// and counters live inside the job's <c>ExecutionState</c>; commits are
/// per-batch so a crash mid-batch leaves a valid resume point.
/// </summary>
public sealed class BulkImportJobHandler(
    IDbContextFactory<AppDbContext> dbFactory,
    IBulkImportCorpusReader reader,
    HeuristicFilter heuristic,
    ImportPayloadFactory payloadFactory,
    IOptions<BulkImportSettings> options,
    ILogger<BulkImportJobHandler> logger)
{
    private readonly BulkImportSettings _config = options.Value;

    public async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        BulkImportParams settings;
        BulkImportState state;

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);
            settings = JsonSerializer.Deserialize(job.Settings, BackgroundJobJsonContext.Default.BulkImportParams)
                ?? throw new InvalidOperationException($"Job {jobId} has empty BulkImport params.");
            state = job.ExecutionState is null
                ? new BulkImportState(null, 0, 0, 0, null)
                : JsonSerializer.Deserialize(job.ExecutionState, BackgroundJobJsonContext.Default.BulkImportState)
                  ?? new BulkImportState(null, 0, 0, 0, null);
        }

        var snapshotsPresent = await reader.SnapshotsExistAsync(
            settings.Language, settings.WiktionarySource, settings.WordfreqSource, ct);

        if (!snapshotsPresent)
        {
            await MarkFailedAsync(
                jobId,
                "Corpus snapshots no longer present (re-import detected). Submit a fresh job.",
                state,
                ct);
            return;
        }

        var query = new BulkImportCorpusQuery(
            settings.Language,
            settings.WiktionarySource,
            settings.WordfreqSource,
            settings.TopFrequencyRank,
            state.Cursor?.LastRank,
            state.Cursor?.LastWord,
            state.Cursor?.LastPos);

        var batch = new List<ImportCandidate>(_config.BatchSize);

        try
        {
            await foreach (var bundle in reader.StreamBundlesAsync(query, ct))
            {
                ct.ThrowIfCancellationRequested();

                var verdict = heuristic.Evaluate(bundle.Word, bundle.Pos);
                var payload = payloadFactory.Build(bundle);
                batch.Add(new ImportCandidate(bundle, verdict, payload));

                if (batch.Count < _config.BatchSize && !LimitReached(state, batch, settings.Limit))
                    continue;

                state = await CommitBatchAsync(jobId, batch, state, ct);
                batch.Clear();

                if (LimitReached(state, batch, settings.Limit))
                    break;

                if (await IsCancelledAsync(jobId, ct))
                {
                    logger.LogInformation("Bulk import job {JobId} cancelled by operator.", jobId);
                    return;
                }
            }

            if (batch.Count > 0)
                state = await CommitBatchAsync(jobId, batch, state, ct);

            await MarkCompletedAsync(jobId, state, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await PersistStateAsync(jobId, state, ct: CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bulk import job {JobId} failed.", jobId);
            await MarkFailedAsync(jobId, ex.Message, state, CancellationToken.None);
        }
    }

    private static bool LimitReached(BulkImportState state, List<ImportCandidate> batch, int? limit) =>
        limit is { } cap && state.ProcessedCount + batch.Count >= cap;

    private async Task<BulkImportState> CommitBatchAsync(
        Guid jobId,
        List<ImportCandidate> batch,
        BulkImportState state,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var sourceRefIds = batch.Select(x => SourceRefId(x.Bundle)).ToHashSet();
        var existing = await db.ImportEntries
            .Where(x => x.Source == EntrySource.Wiktionary && sourceRefIds.Contains(x.SourceRefId))
            .Select(x => x.SourceRefId)
            .ToListAsync(ct);

        var existingSet = existing.ToHashSet();
        var now = DateTimeOffset.UtcNow;
        var accepted = 0;
        var rejected = 0;

        foreach (var candidate in batch)
        {
            var refId = SourceRefId(candidate.Bundle);
            if (existingSet.Contains(refId))
                continue;

            db.ImportEntries.Add(new ImportEntry
            {
                Id = Guid.CreateVersion7(),
                Source = EntrySource.Wiktionary,
                SourceRefId = refId,
                Language = candidate.Bundle.Language,
                Text = candidate.Bundle.Word,
                PartOfSpeech = candidate.Bundle.Pos,
                Payload = candidate.Payload,
                Status = candidate.Verdict.Accepted
                    ? ImportEntryStatus.HeuristicAccepted
                    : ImportEntryStatus.HeuristicRejected,
                StatusReason = candidate.Verdict.Reason,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            if (candidate.Verdict.Accepted)
                accepted++;
            else
                rejected++;
        }

        var last = batch[^1];
        var newCursor = new BulkImportCursor(last.Bundle.Rank, last.Bundle.Word, last.Bundle.Pos);
        var newState = new BulkImportState(
            newCursor,
            state.ProcessedCount + batch.Count,
            state.HeuristicAcceptedCount + accepted,
            state.HeuristicRejectedCount + rejected,
            null);

        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);
        job.ExecutionState = JsonSerializer.Serialize(newState, BackgroundJobJsonContext.Default.BulkImportState);
        job.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Bulk import job {JobId} committed batch: processed={Processed} accepted={Accepted} rejected={Rejected}",
            jobId, newState.ProcessedCount, newState.HeuristicAcceptedCount, newState.HeuristicRejectedCount);

        return newState;
    }

    private async Task<bool> IsCancelledAsync(Guid jobId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var status = await db.BackgroundJobs
            .Where(x => x.Id == jobId)
            .Select(x => x.Status)
            .FirstAsync(ct);

        return status == JobStatus.Cancelled;
    }

    private async Task MarkCompletedAsync(Guid jobId, BulkImportState state, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);
        var now = DateTimeOffset.UtcNow;

        job.Status = JobStatus.Completed;
        job.FinishedAtUtc = now;
        job.UpdatedAtUtc = now;
        job.ExecutionState = JsonSerializer.Serialize(
            state with { Cursor = null, ErrorMessage = null },
            BackgroundJobJsonContext.Default.BulkImportState);

        await db.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAsync(Guid jobId, string error, BulkImportState state, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);
        var now = DateTimeOffset.UtcNow;

        job.Status = JobStatus.Failed;
        job.FinishedAtUtc = now;
        job.UpdatedAtUtc = now;
        job.ExecutionState = JsonSerializer.Serialize(
            state with { ErrorMessage = error },
            BackgroundJobJsonContext.Default.BulkImportState);

        await db.SaveChangesAsync(ct);
    }

    private async Task PersistStateAsync(Guid jobId, BulkImportState state, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);

        job.ExecutionState = JsonSerializer.Serialize(state, BackgroundJobJsonContext.Default.BulkImportState);
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private static string SourceRefId(WiktionaryBundle bundle) =>
        $"{bundle.Language}:{bundle.Word}:{bundle.Pos}";

    private sealed record ImportCandidate(WiktionaryBundle Bundle, HeuristicVerdict Verdict, string Payload);
}
