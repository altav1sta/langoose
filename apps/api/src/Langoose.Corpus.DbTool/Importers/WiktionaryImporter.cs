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
/// </summary>
public sealed class WiktionaryImporter(
    NpgsqlDataSource dataSource,
    string langCode,
    string sourceVersion,
    long? entryLimit = null,
    bool deferIndexes = false) : ICorpusImporter
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

        // 1. Replace any existing rows for this language. Skipped in bulk
        //    mode (--defer-indexes) — the caller is expected to have run
        //    `reset-wiktionary` before the loop, so the table is already
        //    empty; a per-language DELETE would just scan for nothing.
        var deleteElapsed = TimeSpan.Zero;
        long deletedRows = 0;
        if (!deferIndexes)
        {
            var deleteStopwatch = Stopwatch.StartNew();
            Console.WriteLine($"[{Name}] Clearing existing rows for lang_code={langCode}...");
            await using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText =
                    "DELETE FROM wiktionary_entries WHERE lang_code = @lang_code";
                deleteCommand.Parameters.AddWithValue("lang_code", langCode);
                // Large-language DELETEs can easily run past the Npgsql
                // default 30s command timeout. This step is bounded by
                // user's ctrl-c.
                deleteCommand.CommandTimeout = 0;

                deletedRows = await deleteCommand.ExecuteNonQueryAsync(ct);
            }
            deleteElapsed = deleteStopwatch.Elapsed;
            Console.WriteLine(
                $"[{Name}] Cleared {deletedRows:N0} rows in {FormatDuration(deleteElapsed)}");
        }

        // 2. Drop indexes before COPY so we don't pay per-row GIN maintenance.
        //    The JSONB GIN index is especially expensive incrementally (~50-100
        //    path inserts per Wiktionary entry); building it in bulk after
        //    COPY is typically 10-30× faster. Skipped in bulk mode — the
        //    `reset-wiktionary` step run before the bulk loop has already
        //    dropped them, so repeating here would be a no-op.
        var dropIndexElapsed = TimeSpan.Zero;
        if (!deferIndexes)
        {
            Console.WriteLine($"[{Name}] Dropping indexes (rebuilt after COPY)...");
            dropIndexElapsed = await WiktionaryIndexMaintenance.DropAsync(
                connection, transaction, ct);
        }

        // 3. Stream JSONL and bulk-COPY entries.
        Console.WriteLine($"[{Name}] Streaming {sourcePath}...");
        var copyStopwatch = Stopwatch.StartNew();
        var entriesImported = await CopyEntriesAsync(connection, transaction, sourcePath, ct);
        var copyElapsed = copyStopwatch.Elapsed;
        Console.WriteLine(
            $"[{Name}] COPY complete: {entriesImported:N0} entries in {FormatDuration(copyElapsed)} ({RateOrDash(entriesImported, copyElapsed)})");

        // 4. Rebuild indexes — unless the caller is running a multi-language
        //    bulk build and will call `rebuild-indexes` once at the end.
        //    See #97 for the per-language partition follow-up that would
        //    localise the rebuild cost when many languages are loaded.
        TimeSpan buildIndexElapsed = TimeSpan.Zero;
        if (!deferIndexes)
        {
            Console.WriteLine($"[{Name}] Rebuilding indexes...");
            buildIndexElapsed = await WiktionaryIndexMaintenance.CreateAsync(
                connection, transaction, ct);
            Console.WriteLine(
                $"[{Name}] Indexes rebuilt in {FormatDuration(buildIndexElapsed)}");
        }
        else
        {
            Console.WriteLine(
                $"[{Name}] Skipping index rebuild (--defer-indexes set). Run `rebuild-indexes` after all languages are imported.");
        }

        // 5. Record provenance.
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
            metadataCommand.Parameters.AddWithValue("key", $"source_version_{Name}");
            metadataCommand.Parameters.AddWithValue("value", sourceVersion);

            await metadataCommand.ExecuteNonQueryAsync(ct);
        }
        var metadataElapsed = metadataStopwatch.Elapsed;

        // 6. Commit the transaction.
        var commitStopwatch = Stopwatch.StartNew();
        await transaction.CommitAsync(ct);
        var commitElapsed = commitStopwatch.Elapsed;

        var totalElapsed = totalStopwatch.Elapsed;

        Console.WriteLine($"[{Name}] Done in {FormatDuration(totalElapsed)}. Breakdown:");
        if (!deferIndexes)
        {
            Console.WriteLine($"[{Name}]   delete        {FormatDuration(deleteElapsed),10}");
            Console.WriteLine($"[{Name}]   drop indexes  {FormatDuration(dropIndexElapsed),10}");
        }
        Console.WriteLine($"[{Name}]   copy          {FormatDuration(copyElapsed),10}");
        if (!deferIndexes)
            Console.WriteLine($"[{Name}]   build indexes {FormatDuration(buildIndexElapsed),10}");
        Console.WriteLine($"[{Name}]   metadata      {FormatDuration(metadataElapsed),10}");
        Console.WriteLine($"[{Name}]   commit        {FormatDuration(commitElapsed),10}");

        return new ImportSummary(Name, sourceVersion, entriesImported);
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
        CancellationToken ct)
    {
        // BeginBinaryImport doesn't enrol in the transaction directly, but
        // because it shares the connection that's already in a transaction,
        // the writes are part of the same transaction.
        await using var importer = await connection.BeginBinaryImportAsync(
            "COPY wiktionary_entries (lang_code, word, pos, source_version, data) FROM STDIN (FORMAT BINARY)",
            ct);

        long count = 0;
        var stopwatch = Stopwatch.StartNew();
        var nextProgressAt = ProgressIntervalEntries;
        var lastBatchElapsed = TimeSpan.Zero;
        long lastBatchCount = 0;

        await foreach (var entry in StreamEntriesAsync(sourcePath, ct))
        {
            await importer.StartRowAsync(ct);
            await importer.WriteAsync(entry.Data.LangCode, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(entry.Data.Word, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(entry.Data.Pos, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(sourceVersion, NpgsqlDbType.Varchar, ct);
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

    private static async IAsyncEnumerable<RawWiktionaryEntry> StreamEntriesAsync(
        string sourcePath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(sourcePath);

        Stream readStream = sourcePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : fileStream;

        using var reader = new StreamReader(readStream);
        string? line;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            WiktionaryEntry? data;

            try
            {
                data = JsonSerializer.Deserialize(line, CorpusJsonContext.Default.WiktionaryEntry);
            }
            catch (JsonException)
            {
                // Malformed lines are skipped so a single corrupt entry
                // doesn't fail the entire import.
                continue;
            }

            if (data is null
                || string.IsNullOrEmpty(data.Word)
                || string.IsNullOrEmpty(data.LangCode)
                || string.IsNullOrEmpty(data.Pos))
                continue;

            yield return new RawWiktionaryEntry(data, line);
        }

        if (readStream != fileStream)
            await readStream.DisposeAsync();
    }

    private sealed record RawWiktionaryEntry(WiktionaryEntry Data, string RawJson);
}
