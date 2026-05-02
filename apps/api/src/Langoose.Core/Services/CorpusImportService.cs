using System.Text.Json;
using Langoose.Core.Heuristic;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Imports;
using Langoose.Domain.Jobs;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Langoose.Core.Services;

public sealed class CorpusImportService(
    IDbContextFactory<AppDbContext> dbFactory,
    IImportSourceReader reader,
    HeuristicFilter heuristic,
    ILogger<CorpusImportService> logger) : ICorpusImportService
{
    public async Task<BulkJobState> RunBatchAsync(
        CorpusImportParams settings,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (!await reader.SnapshotExistsAsync(settings.Language, settings.Source, cancellationToken))
        {
            throw new InvalidOperationException(
                $"No corpus rows for language '{settings.Language}' at source '{settings.Source}' — typo, language not yet imported, or snapshot rotated. Submit a fresh job.");
        }

        var batch = await reader.FetchBatchAsync(
            settings.Language, settings.Source, batchSize, settings.StartCursor, cancellationToken);

        if (batch.Length == 0)
        {
            // No more data — chain terminates here.
            return new();
        }

        var (insertedCount, skippedCount) = await CommitBatchAsync(batch, cancellationToken);

        // When the batch came back smaller than requested, the source is
        // already exhausted — no point queuing a terminator run that
        // would just fetch zero rows. Signal end-of-chain by leaving
        // Cursor null.
        return new()
        {
            TotalCount = batch.Length,
            ProcessedCount = insertedCount,
            FailedCount = skippedCount,
            Cursor = batch.Length < batchSize ? null : reader.EncodeCursorAfter(batch[^1])
        };
    }

    private async Task<(int Inserted, int Skipped)> CommitBatchAsync(
        ImportPayload[] batch, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var sourceRefIds = batch.Select(SourceRefId).ToHashSet();
        var existing = await db.ImportEntries
            .Where(x => x.Source == EntrySource.Wiktionary && sourceRefIds.Contains(x.SourceRefId))
            .Select(x => x.SourceRefId)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet();
        var now = DateTimeOffset.UtcNow;
        var inserted = 0;
        var skipped = 0;

        foreach (var payload in batch)
        {
            var refId = SourceRefId(payload);

            if (existingSet.Contains(refId))
            {
                skipped++;
                continue;
            }

            var verdict = heuristic.Evaluate(payload.Entry.Text, payload.Entry.Pos);
            var payloadJson = JsonSerializer.Serialize(payload, AppJsonOptions.Default);

            db.ImportEntries.Add(new()
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

            inserted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Corpus import committed batch: inserted={Inserted} skipped={Skipped}",
            inserted, skipped);

        return (inserted, skipped);
    }

    // SourceRefId is the dedup key for ON CONFLICT DO NOTHING; we hash
    // (language, text, pos) so the column stays a fixed 64-char string
    // even when wiktionary publishes very long phrases as headwords.
    private static string SourceRefId(ImportPayload payload)
    {
        var raw = $"{payload.Entry.Language} {payload.Entry.Text} {payload.Entry.Pos}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
