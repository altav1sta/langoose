using Langoose.Domain.Models;

namespace Langoose.Domain.Imports;

/// <summary>
/// Source-agnostic contract for streaming candidate dictionary entries
/// into the bulk-import pipeline. One implementation per source. The
/// reader knows its own source kind via <see cref="SourceName"/>; callers
/// pass primitives only (snapshot identifier, language filter, paging).
/// </summary>
public interface IImportSourceReader
{
    /// <summary>
    /// Identifier of the source this reader handles (e.g. <c>"wiktionary"</c>).
    /// Used for logging.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Validate that the source has at least one row for
    /// <paramref name="language"/> at the snapshot identified by
    /// <paramref name="snapshot"/>. Returning false moves the job to
    /// Failed — guards against typos, languages that were never imported
    /// for this snapshot, or a snapshot that has been rotated out.
    /// </summary>
    Task<bool> SnapshotExistsAsync(string language, string snapshot, CancellationToken ct);

    /// <summary>
    /// Fetch the next batch of typed payloads for the given language and
    /// snapshot, optionally resuming after <paramref name="cursor"/>. Each
    /// call returns up to <paramref name="batchSize"/> payloads in a stable
    /// source-defined order; an empty array signals no more data. The
    /// cursor format is opaque — the handler derives the next cursor from
    /// the last returned payload.
    /// </summary>
    Task<ImportPayload[]> FetchBatchAsync(
        string language,
        string snapshot,
        int batchSize,
        string? cursor,
        CancellationToken ct);
}
