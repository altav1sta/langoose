using System.Text.Json;
using Langoose.Core.BulkImport;
using Langoose.Corpus.Data.Readers;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Imports;
using Langoose.Domain.Jobs;
using Langoose.Domain.Models;
using Langoose.Worker.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Langoose.Worker.Jobs;

/// <summary>
/// Executes one <see cref="JobType.BulkImport"/> job end to end: fetches
/// batches of typed payloads from the configured
/// <see cref="IImportSourceReader"/>, applies the heuristic filter, and
/// writes <see cref="ImportEntry"/> rows. Cursor and counters live inside
/// the job's <c>ExecutionState</c>; commits are per-batch so a crash
/// mid-batch leaves a valid resume point.
/// </summary>
public sealed class BulkImportJobHandler(
    IDbContextFactory<AppDbContext> dbFactory,
    IImportSourceReader reader,
    HeuristicFilter heuristic,
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
            settings = JsonSerializer.Deserialize<BulkImportParams>(job.Settings, AppJsonOptions.Default)
                ?? throw new InvalidOperationException($"Job {jobId} has empty BulkImport params.");
            state = job.ExecutionState is null
                ? new BulkImportState(null, 0, 0, 0, null)
                : JsonSerializer.Deserialize<BulkImportState>(job.ExecutionState, AppJsonOptions.Default)
                  ?? new BulkImportState(null, 0, 0, 0, null);
        }

        if (!await reader.SnapshotExistsAsync(settings.Source, ct))
        {
            await MarkFailedAsync(
                jobId,
                $"Source snapshot '{settings.Source}' no longer present (re-import detected). Submit a fresh job.",
                state,
                ct);
            return;
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var batch = await reader.FetchBatchAsync(
                    settings.Language, settings.Source, _config.BatchSize, state.Cursor, ct);

                if (batch.Length == 0)
                    break;

                state = await CommitBatchAsync(jobId, batch, state, ct);

                if (await IsCancelledAsync(jobId, ct))
                {
                    logger.LogInformation("Bulk import job {JobId} cancelled by operator.", jobId);
                    return;
                }
            }

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

    private async Task<BulkImportState> CommitBatchAsync(
        Guid jobId,
        ImportPayload[] batch,
        BulkImportState state,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var sourceRefIds = batch.Select(SourceRefId).ToHashSet();
        var existing = await db.ImportEntries
            .Where(x => x.Source == EntrySource.Wiktionary && sourceRefIds.Contains(x.SourceRefId))
            .Select(x => x.SourceRefId)
            .ToListAsync(ct);

        var existingSet = existing.ToHashSet();
        var now = DateTimeOffset.UtcNow;
        var accepted = 0;
        var rejected = 0;

        foreach (var payload in batch)
        {
            var refId = SourceRefId(payload);
            if (existingSet.Contains(refId))
                continue;

            var verdict = heuristic.Evaluate(payload.Entry.Text, payload.Entry.Pos);
            var payloadJson = JsonSerializer.Serialize(payload, AppJsonOptions.Default);

            db.ImportEntries.Add(new ImportEntry
            {
                Id = Guid.CreateVersion7(),
                Source = EntrySource.Wiktionary,
                SourceRefId = refId,
                Language = payload.Entry.Language,
                Text = payload.Entry.Text,
                PartOfSpeech = payload.Entry.Pos,
                Payload = payloadJson,
                Status = verdict.Accepted
                    ? ImportEntryStatus.HeuristicAccepted
                    : ImportEntryStatus.HeuristicRejected,
                StatusReason = verdict.Reason,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            if (verdict.Accepted)
                accepted++;
            else
                rejected++;
        }

        var last = batch[^1];
        var newCursor = WiktionaryImportSourceReader.EncodeCursor(last.Entry.Text, last.Entry.Pos);
        var newState = new BulkImportState(
            newCursor,
            state.ProcessedCount + batch.Length,
            state.HeuristicAcceptedCount + accepted,
            state.HeuristicRejectedCount + rejected,
            null);

        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);
        job.ExecutionState = JsonSerializer.Serialize(newState, AppJsonOptions.Default);
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
            AppJsonOptions.Default);

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
            AppJsonOptions.Default);

        await db.SaveChangesAsync(ct);
    }

    private async Task PersistStateAsync(Guid jobId, BulkImportState state, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.BackgroundJobs.FirstAsync(x => x.Id == jobId, ct);

        job.ExecutionState = JsonSerializer.Serialize(state, AppJsonOptions.Default);
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private static string SourceRefId(ImportPayload payload) =>
        $"{payload.Entry.Language}:{payload.Entry.Text}:{payload.Entry.Pos}";
}
