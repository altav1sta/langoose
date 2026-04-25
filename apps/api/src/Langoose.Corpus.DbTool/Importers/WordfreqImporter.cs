using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;

namespace Langoose.Corpus.DbTool.Importers;

/// <summary>
/// Imports a wordfreq frequency-ranking TSV for a single language into the
/// corpus database. Streams the input, bulk-loads via
/// <see cref="NpgsqlBinaryImporter"/>, and replaces any existing rows with
/// the same (lang_code, source) tuple in a single transaction.
///
/// Input format: tab-separated, no header, three columns per line —
/// <c>word\trank\tzipf_score</c>. Plain-text or .gz are both accepted.
/// Fetch with <c>scripts/download-wordfreq.sh &lt;lang&gt; &lt;out-path&gt;</c>.
/// </summary>
public sealed class WordfreqImporter(
    NpgsqlDataSource dataSource,
    string langCode,
    string sourceVersion) : ICorpusImporter
{
    public string Name => $"wordfreq_{langCode}";

    public async Task<ImportSummary> ImportAsync(string sourcePath, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);

        var totalStopwatch = Stopwatch.StartNew();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // 1. Replace existing rows for this (lang, source) pair. The PK is
        //    (lang_code, word, source), so a re-import with the same source
        //    string would otherwise hit a uniqueness violation.
        var deleteStopwatch = Stopwatch.StartNew();
        long deletedRows;
        Console.WriteLine($"[{Name}] Clearing existing rows for lang_code={langCode}, source={sourceVersion}...");
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText =
                "DELETE FROM wordfreq_rankings WHERE lang_code = @lang_code AND source = @source";
            deleteCommand.Parameters.AddWithValue("lang_code", langCode);
            deleteCommand.Parameters.AddWithValue("source", sourceVersion);
            deleteCommand.CommandTimeout = 0;

            deletedRows = await deleteCommand.ExecuteNonQueryAsync(ct);
        }
        var deleteElapsed = deleteStopwatch.Elapsed;
        Console.WriteLine(
            $"[{Name}] Cleared {deletedRows:N0} rows in {FormatDuration(deleteElapsed)}");

        // 2. Stream TSV and bulk-COPY rankings. wordfreq lists are small
        //    (top-N for any reasonable N is at most low six figures), so
        //    we don't bother with index drop/rebuild — the (lang_code, rank)
        //    btree maintenance during COPY is negligible at this scale.
        Console.WriteLine($"[{Name}] Streaming {sourcePath}...");
        var copyStopwatch = Stopwatch.StartNew();
        var rowsImported = await CopyRowsAsync(connection, transaction, sourcePath, ct);
        var copyElapsed = copyStopwatch.Elapsed;
        Console.WriteLine(
            $"[{Name}] COPY complete: {rowsImported:N0} rows in {FormatDuration(copyElapsed)} ({RateOrDash(rowsImported, copyElapsed)})");

        // 3. Record provenance.
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

        await transaction.CommitAsync(ct);

        Console.WriteLine($"[{Name}] Done in {FormatDuration(totalStopwatch.Elapsed)}.");

        return new ImportSummary(Name, sourceVersion, rowsImported);
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

    private async Task<long> CopyRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourcePath,
        CancellationToken ct)
    {
        await using var importer = await connection.BeginBinaryImportAsync(
            "COPY wordfreq_rankings (lang_code, word, rank, zipf_score, source) FROM STDIN (FORMAT BINARY)",
            ct);

        long count = 0;
        await foreach (var row in StreamRowsAsync(sourcePath, ct))
        {
            await importer.StartRowAsync(ct);
            await importer.WriteAsync(langCode, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(row.Word, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(row.Rank, NpgsqlDbType.Integer, ct);
            await importer.WriteAsync(row.ZipfScore, NpgsqlDbType.Numeric, ct);
            await importer.WriteAsync(sourceVersion, NpgsqlDbType.Varchar, ct);
            count++;
        }

        await importer.CompleteAsync(ct);

        return count;
    }

    private async IAsyncEnumerable<ParsedRow> StreamRowsAsync(
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

            var parts = line.Split('\t');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException(
                    $"Malformed TSV on line {lineNumber} of '{sourcePath}': expected 3 tab-separated columns " +
                    $"(word, rank, zipf_score) but got {parts.Length}. Line preview: {Preview(line)}.");
            }

            var word = parts[0];
            if (string.IsNullOrEmpty(word))
            {
                throw new InvalidOperationException(
                    $"Empty word on line {lineNumber} of '{sourcePath}'. Line preview: {Preview(line)}.");
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)
                || rank < 1)
            {
                throw new InvalidOperationException(
                    $"Invalid rank on line {lineNumber} of '{sourcePath}': '{parts[1]}'. " +
                    $"Expected a positive integer (1 = most frequent). Line preview: {Preview(line)}.");
            }

            if (!decimal.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var zipfScore))
            {
                throw new InvalidOperationException(
                    $"Invalid zipf_score on line {lineNumber} of '{sourcePath}': '{parts[2]}'. " +
                    $"Expected a decimal number on the wordfreq Zipf scale (~0–8). Line preview: {Preview(line)}.");
            }

            yield return new ParsedRow(word, rank, zipfScore);
        }

        if (readStream != fileStream)
            await readStream.DisposeAsync();
    }

    private static string Preview(string line) =>
        line.Length > 200 ? line[..200] + "..." : line;

    private sealed record ParsedRow(string Word, int Rank, decimal ZipfScore);
}
