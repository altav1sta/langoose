using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Langoose.Corpus.Data;
using Langoose.Corpus.Data.Models;
using Npgsql;
using NpgsqlTypes;

namespace Langoose.Corpus.DbTool.Importers;

/// <summary>
/// Imports a Kaikki Wiktionary JSONL extract for a single language into the
/// corpus database. Streams the input (no full-file load), bulk-loads via
/// <see cref="NpgsqlBinaryImporter"/>, and replaces any existing rows for
/// the same language in a single transaction.
///
/// Per #97 the table is LIST-partitioned by <c>lang_code</c>: this importer
/// ensures the per-language partition exists, drops/rebuilds only that
/// partition's indexes, and TRUNCATEs the partition rather than scanning
/// the whole table for a per-row DELETE. Other languages' partitions are
/// untouched.
/// </summary>
public sealed class WiktionaryImporter(
    NpgsqlDataSource dataSource,
    string langCode,
    string source,
    long? entryLimit = null,
    bool deferIndexes = false,
    int? frequencyFilterTop = null) : ICorpusImporter
{
    private const long ProgressIntervalEntries = 50_000;

    public string Name => $"wiktionary_{langCode}";

    public async Task<ImportSummary> ImportAsync(string sourcePath, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);

        var totalStopwatch = Stopwatch.StartNew();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // 1. Make sure this language's partition exists. Idempotent — re-imports
        //    under the same lang_code reuse it. In the bulk (--defer-indexes)
        //    flow `reset-wiktionary` will have dropped any pre-existing
        //    partition first, so this is also where partitions come back
        //    after a reset. Validates lang_code as a safe identifier.
        Console.WriteLine($"[{Name}] Ensuring partition {WiktionaryIndexMaintenance.PartitionName(langCode)} exists...");
        await WiktionaryIndexMaintenance.EnsurePartitionAsync(connection, transaction, langCode, ct);

        // 2. Drop the partition's indexes before COPY so we don't pay
        //    per-row GIN maintenance. The JSONB GIN index is especially
        //    expensive incrementally (~50-100 path inserts per Wiktionary
        //    entry); building it in bulk after COPY is typically 10-30×
        //    faster. Skipped in --defer-indexes mode — reset-wiktionary
        //    drops all partitions before the bulk loop, so the freshly
        //    created partition has no indexes to drop.
        var dropIndexElapsed = TimeSpan.Zero;
        var truncateElapsed = TimeSpan.Zero;
        if (!deferIndexes)
        {
            Console.WriteLine($"[{Name}] Dropping partition indexes (rebuilt after COPY)...");
            dropIndexElapsed = await WiktionaryIndexMaintenance.DropAsync(
                connection, transaction, langCode, ct);

            // 3. TRUNCATE the partition. Constant-time replacement for the
            //    pre-#97 per-row DELETE — TRUNCATE skips the scan and
            //    reclaims space immediately, and it only affects this
            //    language's rows.
            var truncateStopwatch = Stopwatch.StartNew();
            Console.WriteLine($"[{Name}] Truncating partition for lang_code={langCode}...");
            await WiktionaryIndexMaintenance.TruncatePartitionAsync(
                connection, transaction, langCode, ct);
            truncateElapsed = truncateStopwatch.Elapsed;
        }

        // 4. Optionally load a frequency-ranked allowlist. Used by mini-dump
        //    builds to keep entries representative of everyday vocabulary
        //    instead of taking the first N entries (which Kaikki publishes
        //    in roughly alphabetical order, biased toward `a-`/`ab-`).
        HashSet<string>? frequencyAllowlist = null;
        if (frequencyFilterTop is { } topN)
        {
            frequencyAllowlist = await LoadFrequencyAllowlistAsync(
                connection, transaction, topN, ct);
            Console.WriteLine(
                $"[{Name}] Frequency filter active: keeping only entries whose word is in the top {topN:N0} of wordfreq_rankings ({frequencyAllowlist.Count:N0} distinct words loaded).");

            if (frequencyAllowlist.Count == 0)
            {
                throw new InvalidOperationException(
                    $"--frequency-filter-top {topN} was requested but wordfreq_rankings " +
                    $"has no rows for lang_code='{langCode}'. Run import-wordfreq for " +
                    $"this language first, or omit the flag to import without filtering.");
            }
        }

        // 5. Stream JSONL and bulk-COPY entries. COPY targets the parent
        //    table; Postgres routes each row to its partition by lang_code.
        Console.WriteLine($"[{Name}] Streaming {sourcePath}...");
        var copyStopwatch = Stopwatch.StartNew();
        var entriesImported = await CopyEntriesAsync(
            connection, transaction, sourcePath, frequencyAllowlist, ct);
        var copyElapsed = copyStopwatch.Elapsed;
        Console.WriteLine(
            $"[{Name}] COPY complete: {entriesImported:N0} entries in {FormatDuration(copyElapsed)} ({RateOrDash(entriesImported, copyElapsed)})");

        // 6. Rebuild the partition's indexes — unless the caller is running
        //    a multi-language bulk build and will call `rebuild-indexes`
        //    once at the end. Per-partition rebuild only scans this
        //    language's rows, which is the whole point of #97.
        TimeSpan buildIndexElapsed = TimeSpan.Zero;
        if (!deferIndexes)
        {
            Console.WriteLine($"[{Name}] Rebuilding partition indexes...");
            buildIndexElapsed = await WiktionaryIndexMaintenance.CreateAsync(
                connection, transaction, langCode, ct);
            Console.WriteLine(
                $"[{Name}] Indexes rebuilt in {FormatDuration(buildIndexElapsed)}");
        }
        else
        {
            Console.WriteLine(
                $"[{Name}] Skipping index rebuild (--defer-indexes set). Run `rebuild-indexes` after all languages are imported.");
        }

        // 7. Record provenance.
        var metadataStopwatch = Stopwatch.StartNew();
        await using (var metadataCommand = connection.CreateCommand())
        {
            metadataCommand.Transaction = transaction;
            metadataCommand.CommandText = """
                INSERT INTO corpus_metadata (key, value, updated_at_utc)
                VALUES (@key, @value, NOW())
                ON CONFLICT (key) DO UPDATE
                SET value = EXCLUDED.value, updated_at_utc = NOW()
                """;
            metadataCommand.Parameters.AddWithValue("key", $"source_{Name}");
            metadataCommand.Parameters.AddWithValue("value", source);

            await metadataCommand.ExecuteNonQueryAsync(ct);
        }
        var metadataElapsed = metadataStopwatch.Elapsed;

        // 8. Commit the transaction.
        var commitStopwatch = Stopwatch.StartNew();
        await transaction.CommitAsync(ct);
        var commitElapsed = commitStopwatch.Elapsed;

        var totalElapsed = totalStopwatch.Elapsed;

        Console.WriteLine($"[{Name}] Done in {FormatDuration(totalElapsed)}. Breakdown:");
        if (!deferIndexes)
        {
            Console.WriteLine($"[{Name}]   drop indexes  {FormatDuration(dropIndexElapsed),10}");
            Console.WriteLine($"[{Name}]   truncate      {FormatDuration(truncateElapsed),10}");
        }
        Console.WriteLine($"[{Name}]   copy          {FormatDuration(copyElapsed),10}");
        if (!deferIndexes)
            Console.WriteLine($"[{Name}]   build indexes {FormatDuration(buildIndexElapsed),10}");
        Console.WriteLine($"[{Name}]   metadata      {FormatDuration(metadataElapsed),10}");
        Console.WriteLine($"[{Name}]   commit        {FormatDuration(commitElapsed),10}");

        return new ImportSummary(Name, source, entriesImported);
    }

    private static string FormatDuration(TimeSpan elapsed) =>
        elapsed.TotalSeconds < 1
            ? $"{elapsed.TotalMilliseconds:N0}ms"
            : elapsed.TotalSeconds < 60
                ? $"{elapsed.TotalSeconds:N1}s"
                : $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds:D2}s";

    private static string RateOrDash(long count, TimeSpan elapsed) =>
        elapsed.TotalSeconds > 0
            ? $"{count / elapsed.TotalSeconds:N0}/s"
            : "—";

    private async Task<long> CopyEntriesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourcePath,
        HashSet<string>? frequencyAllowlist,
        CancellationToken ct)
    {
        // BeginBinaryImport doesn't enrol in the transaction directly, but
        // because it shares the connection that's already in a transaction,
        // the writes are part of the same transaction.
        await using var importer = await connection.BeginBinaryImportAsync(
            "COPY wiktionary_entries (lang_code, word, pos, source, data) FROM STDIN (FORMAT BINARY)",
            ct);

        long count = 0;
        var stopwatch = Stopwatch.StartNew();
        var nextProgressAt = ProgressIntervalEntries;
        var lastBatchElapsed = TimeSpan.Zero;
        long lastBatchCount = 0;

        await foreach (var entry in StreamEntriesAsync(sourcePath, ct))
        {
            if (frequencyAllowlist is not null
                && !frequencyAllowlist.Contains(entry.Data.Word))
                continue;

            await importer.StartRowAsync(ct);
            // langCode (the constructor argument) is the single source of
            // truth for the stored column. The source entry's lang_code is
            // validated to match in StreamEntriesAsync, so these are
            // always equal here — writing the constructor value keeps the
            // partition routing and metadata stamp unambiguously consistent.
            await importer.WriteAsync(langCode, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(entry.Data.Word, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(entry.Data.Pos, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(source, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(entry.RawJson, NpgsqlDbType.Jsonb, ct);

            count++;

            if (count >= nextProgressAt)
            {
                var now = stopwatch.Elapsed;
                var batchElapsed = now - lastBatchElapsed;
                var batchCount = count - lastBatchCount;
                Console.WriteLine(
                    $"[{Name}]   {count:N0} entries streamed (batch {FormatDuration(batchElapsed)} @ {RateOrDash(batchCount, batchElapsed)})");
                lastBatchElapsed = now;
                lastBatchCount = count;
                nextProgressAt += ProgressIntervalEntries;
            }

            if (entryLimit is { } cap && count >= cap)
                break;
        }

        await importer.CompleteAsync(ct);

        return count;
    }

    private async Task<HashSet<string>> LoadFrequencyAllowlistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int topN,
        CancellationToken ct)
    {
        // DISTINCT collapses the same word ranked under multiple sources
        // (e.g. wordfreq-large + a future SUBTLEX import). The set is
        // tiny — even N=100k is < 5 MB in memory — so a HashSet lookup
        // per Wiktionary entry is the cheap path.
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT DISTINCT word FROM wordfreq_rankings
            WHERE lang_code = @lang_code AND rank <= @top_n
            """;
        command.Parameters.AddWithValue("lang_code", langCode);
        command.Parameters.AddWithValue("top_n", topN);
        command.CommandTimeout = 0;

        var allowlist = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            allowlist.Add(reader.GetString(0));

        return allowlist;
    }

    private async IAsyncEnumerable<RawWiktionaryEntry> StreamEntriesAsync(
        string sourcePath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(sourcePath);

        Stream readStream = sourcePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : fileStream;

        using var reader = new StreamReader(readStream);
        string? line;
        long lineNumber = 0;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Strict parsing. Silent skips on a release-building pipeline
            // would be indistinguishable from success — a truncated
            // download or a corrupted line could produce a "successful"
            // dump missing entries, with metadata stamped as if nothing
            // was wrong. Fail fast with line number + preview so the
            // operator can inspect or re-download.
            WiktionaryEntry? data;
            try
            {
                data = JsonSerializer.Deserialize(line, CorpusJsonContext.Default.WiktionaryEntry);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Malformed JSON on line {lineNumber} of '{sourcePath}': {ex.Message}. " +
                    $"Line preview: {Preview(line)}. " +
                    $"Re-download the source file or inspect the line.",
                    ex);
            }

            if (data is null)
            {
                throw new InvalidOperationException(
                    $"Line {lineNumber} of '{sourcePath}' deserialised to null. " +
                    $"Line preview: {Preview(line)}.");
            }

            if (string.IsNullOrEmpty(data.Word)
                || string.IsNullOrEmpty(data.LangCode)
                || string.IsNullOrEmpty(data.Pos))
            {
                throw new InvalidOperationException(
                    $"Entry on line {lineNumber} of '{sourcePath}' is missing a required field. " +
                    $"Got word='{data.Word}', lang_code='{data.LangCode}', pos='{data.Pos}'.");
            }

            // Fail fast if the source file's declared lang_code doesn't
            // match the --lang flag. Without this check, the TRUNCATE
            // (keyed on the constructor's langCode partition) and the COPY
            // (would otherwise route the source's lang_code into a
            // different partition) could disagree — existing rows of
            // langCode wiped, new rows landing under a different code,
            // metadata stamped under the wrong key. Transaction rollback
            // in the caller restores pre-import state cleanly.
            if (!string.Equals(data.LangCode, langCode, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Source file '{sourcePath}' contains entry '{data.Word}' with lang_code='{data.LangCode}', " +
                    $"but --lang was set to '{langCode}'. Either pass --lang {data.LangCode} or use a matching source file.");
            }

            yield return new RawWiktionaryEntry(data, line);
        }

        if (readStream != fileStream)
            await readStream.DisposeAsync();
    }

    private static string Preview(string line) =>
        line.Length > 200 ? line[..200] + "..." : line;

    private sealed record RawWiktionaryEntry(WiktionaryEntry Data, string RawJson);
}
