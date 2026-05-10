using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;

namespace Langoose.Corpus.DbTool.Importers;

/// <summary>
/// Imports a Tatoeba bilingual sentence pair (text only — audio is
/// CC-BY-NC and excluded) into the corpus database. Streams the per-
/// language sentence files plus the global links file in a single
/// transaction.
///
/// Source layout: <paramref name="sourcePath"/> in <see cref="ImportAsync"/>
/// is a directory containing the two per-language sentence files for
/// the chosen pair plus the global Tatoeba links file (plain or .gz):
///
///   <c>&lt;lang&gt;_sentences.tsv</c>      — id\tlang\ttext, lang_code's sentences
///   <c>&lt;pair-lang&gt;_sentences.tsv</c> — id\tlang\ttext, pair_lang_code's sentences
///   <c>links.tsv</c>                       — source_id\ttarget_id, the global links table
///
/// Where &lt;lang&gt; / &lt;pair-lang&gt; are the 2-letter ISO codes passed
/// to the constructor. The downloader
/// (<c>scripts/download-tatoeba.sh</c>) writes files with these codes
/// into a shared <c>data/corpus/tatoeba/</c> directory after mapping
/// from Tatoeba's 3-letter codes (eng/rus/etc.) and decompressing the
/// upstream .bz2 archives — multiple pair imports share one downloaded
/// copy of the global links file. The middle column of each sentence
/// file is ignored: the file is by definition all one language, and
/// trusting the directory layout avoids embedding a 2→3-letter mapping
/// in C#.
///
/// Per #113 <c>tatoeba_sentences</c> is LIST-partitioned by lang_code:
/// this importer ensures both per-language partitions, TRUNCATEs them,
/// and TRUNCATEs <c>tatoeba_links</c> entirely. Multi-pair coexistence
/// (e.g. en-ru and en-de in the same DB) is explicitly out of scope for
/// the first importer (per the issue's "out of scope" list); a later
/// follow-up can add an upsert path if it becomes necessary.
/// </summary>
public sealed class TatoebaImporter(
    NpgsqlDataSource dataSource,
    string langCode,
    string pairLangCode,
    string source,
    long? maxPairs = null) : ICorpusImporter
{
    // Hard cap on cross-language pairs sharing a sentence when
    // --max-pairs is in effect. Two ensures every kept sentence has at
    // least one cross-language partner without letting a few "popular"
    // sentences hog the budget. Not configurable via CLI: the test-dump
    // filter is naive enough that a knob here would be premature; #114
    // will replace this whole step anyway.
    private const int MaxPairsPerSentence = 2;

    public string Name => $"tatoeba_{langCode}_{pairLangCode}";

    public async Task<ImportSummary> ImportAsync(string sourcePath, CancellationToken ct = default)
    {
        // Validate lang codes up front. Both feed into partition DDL via
        // TatoebaIndexMaintenance, but the file-resolution step below
        // would otherwise throw a less helpful FileNotFoundException for
        // an unsafe code like "en'; DROP TABLE--" before the SQL layer
        // ever sees it.
        _ = TatoebaIndexMaintenance.PartitionName(langCode);
        _ = TatoebaIndexMaintenance.PartitionName(pairLangCode);

        if (string.Equals(langCode, pairLangCode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"--lang and --pair-lang must differ; both were '{langCode}'.",
                nameof(pairLangCode));
        }

        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException(
                $"Tatoeba source directory not found: {sourcePath}. " +
                $"Expected a directory containing {langCode}_sentences.tsv, " +
                $"{pairLangCode}_sentences.tsv, and links.tsv " +
                $"(produced by scripts/download-tatoeba.sh).");
        }

        var langSentencesPath = ResolveFile(sourcePath, $"{langCode}_sentences.tsv");
        var pairSentencesPath = ResolveFile(sourcePath, $"{pairLangCode}_sentences.tsv");
        var linksPath = ResolveFile(sourcePath, "links.tsv");

        var totalStopwatch = Stopwatch.StartNew();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // 1. Ensure both partitions exist (validates lang_code identifiers).
        Console.WriteLine($"[{Name}] Ensuring partition {TatoebaIndexMaintenance.PartitionName(langCode)} exists...");
        await TatoebaIndexMaintenance.EnsurePartitionAsync(connection, transaction, langCode, ct);
        Console.WriteLine($"[{Name}] Ensuring partition {TatoebaIndexMaintenance.PartitionName(pairLangCode)} exists...");
        await TatoebaIndexMaintenance.EnsurePartitionAsync(connection, transaction, pairLangCode, ct);

        // 2. Wipe both partitions and the links table. Multi-pair coexistence
        //    is out of scope (#113); each import owns the whole tatoeba_*
        //    namespace for the duration of one transaction.
        Console.WriteLine($"[{Name}] Truncating partitions and links table...");
        await TatoebaIndexMaintenance.TruncatePartitionAsync(connection, transaction, langCode, ct);
        await TatoebaIndexMaintenance.TruncatePartitionAsync(connection, transaction, pairLangCode, ct);

        await using (var truncateLinksCommand = connection.CreateCommand())
        {
            truncateLinksCommand.Transaction = transaction;
            truncateLinksCommand.CommandText = "TRUNCATE TABLE tatoeba_links";
            truncateLinksCommand.CommandTimeout = 0;
            await truncateLinksCommand.ExecuteNonQueryAsync(ct);
        }

        long sentencesImported;
        long linksImported;

        if (maxPairs is { } budget)
        {
            // Pre-filter flow: decide the kept pair set in memory before
            // touching the DB, then COPY only the sentences those pairs
            // reference. Avoids importing ~2M sentences just to delete
            // 99.9% of them on the way to a small test dump.
            //
            // Pair, not link: each cross-language pair contributes both
            // directional rows from the source file (typically (a→b) and
            // (b→a)) so a downstream lookup by source_id finds the
            // translation in either direction without a UNION. The
            // budget is a *pair* count; the resulting link-row count is
            // typically 2× pairs.
            //
            // TODO(#114): once the lemmatizer lands, replace this naive
            // first-N-by-(canonical pair) selector with a lemma-
            // frequency filter on sentences (the selected sentences
            // will naturally have rich link coverage). Tracked in
            // docs/agent/parallel-corpora.md.

            // Phase A: scan sentence IDs from both files into HashSets.
            //          Streams the files, doesn't COPY.
            Console.WriteLine($"[{Name}] Pre-filter (budget {budget:N0} pair(s), max {MaxPairsPerSentence}/sentence)");
            Console.WriteLine($"[{Name}]   Phase A: scanning sentence IDs from {langSentencesPath} + {pairSentencesPath}...");
            var phaseAStopwatch = Stopwatch.StartNew();
            var langIds = await ScanSentenceIdsAsync(langSentencesPath, ct);
            var pairIds = await ScanSentenceIdsAsync(pairSentencesPath, ct);
            Console.WriteLine(
                $"[{Name}]   Phase A done: {langIds.Count:N0} {langCode} + {pairIds.Count:N0} {pairLangCode} sentence IDs in {FormatDuration(phaseAStopwatch.Elapsed)}");

            // Phase B+C: stream the global links file, group by
            //            canonical pair, sort, apply per-sentence cap +
            //            budget. Output is keptLinks (typically 2×
            //            keptPairs rows) and keptSentenceIds.
            Console.WriteLine($"[{Name}]   Phase B+C: collecting cross-pair links from {linksPath} and applying budget...");
            var phaseBcStopwatch = Stopwatch.StartNew();
            var (keptLinks, keptSentenceIds, keptPairCount) =
                await SelectKeptLinksAsync(linksPath, langIds, pairIds, budget, ct);
            Console.WriteLine(
                $"[{Name}]   Phase B+C done: {keptPairCount:N0} pair(s) → {keptLinks.Count:N0} link row(s) referencing {keptSentenceIds.Count:N0} sentence(s) in {FormatDuration(phaseBcStopwatch.Elapsed)}");

            // Phase D: re-stream sentence files, COPY only rows whose ID
            //          is in keptSentenceIds.
            Console.WriteLine($"[{Name}]   Phase D: COPYing kept sentences only...");
            var phaseDStopwatch = Stopwatch.StartNew();
            var langCopied = await CopySentencesAsync(connection, langSentencesPath, langCode, keptSentenceIds, ct);
            var pairCopied = await CopySentencesAsync(connection, pairSentencesPath, pairLangCode, keptSentenceIds, ct);
            Console.WriteLine(
                $"[{Name}]   Phase D done: {langCopied.Count:N0} {langCode} + {pairCopied.Count:N0} {pairLangCode} sentences in {FormatDuration(phaseDStopwatch.Elapsed)}");

            // Phase E: COPY the in-memory kept links list.
            Console.WriteLine($"[{Name}]   Phase E: COPYing kept links...");
            var phaseEStopwatch = Stopwatch.StartNew();
            await CopyKeptLinksAsync(connection, keptLinks, ct);
            Console.WriteLine(
                $"[{Name}]   Phase E done: {keptLinks.Count:N0} links in {FormatDuration(phaseEStopwatch.Elapsed)}");

            sentencesImported = langCopied.Count + pairCopied.Count;
            linksImported = keptLinks.Count;
        }
        else
        {
            // Full-import flow: COPY every sentence and every cross-pair
            // link. No in-memory state besides the streaming HashSets.

            // 3. Stream and COPY the lang_code partition's sentences.
            //    Track every imported ID — used to filter the global
            //    links file.
            Console.WriteLine($"[{Name}] Streaming {langSentencesPath}...");
            var langCopyStopwatch = Stopwatch.StartNew();
            var langIds = await CopySentencesAsync(connection, langSentencesPath, langCode, filter: null, ct);
            Console.WriteLine(
                $"[{Name}] {langCode}: {langIds.Count:N0} sentences in {FormatDuration(langCopyStopwatch.Elapsed)}");

            // 4. Same for the pair partition.
            Console.WriteLine($"[{Name}] Streaming {pairSentencesPath}...");
            var pairCopyStopwatch = Stopwatch.StartNew();
            var pairIds = await CopySentencesAsync(connection, pairSentencesPath, pairLangCode, filter: null, ct);
            Console.WriteLine(
                $"[{Name}] {pairLangCode}: {pairIds.Count:N0} sentences in {FormatDuration(pairCopyStopwatch.Elapsed)}");

            // 5. Stream the global links file, keep only links whose
            //    endpoints both landed in the two partitions just
            //    imported, COPY them.
            Console.WriteLine($"[{Name}] Streaming {linksPath} (filtering to imported IDs)...");
            var linksCopyStopwatch = Stopwatch.StartNew();
            linksImported = await CopyLinksAsync(connection, linksPath, langIds, pairIds, ct);
            Console.WriteLine(
                $"[{Name}] {linksImported:N0} links in {FormatDuration(linksCopyStopwatch.Elapsed)}");

            sentencesImported = langIds.Count + pairIds.Count;
        }

        // 6. Record provenance.
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

        await transaction.CommitAsync(ct);

        var totalElapsed = totalStopwatch.Elapsed;
        Console.WriteLine($"[{Name}] Done in {FormatDuration(totalElapsed)}.");

        return new ImportSummary(Name, source, sentencesImported + linksImported);
    }

    private async Task<HashSet<long>> ScanSentenceIdsAsync(
        string sentencesPath,
        CancellationToken ct)
    {
        var ids = new HashSet<long>();
        await foreach (var row in StreamSentencesAsync(sentencesPath, ct))
            ids.Add(row.Id);
        return ids;
    }

    private async Task<(List<LinkRow> KeptLinks, HashSet<long> KeptSentenceIds, long KeptPairCount)> SelectKeptLinksAsync(
        string linksPath,
        HashSet<long> langIds,
        HashSet<long> pairIds,
        long budget,
        CancellationToken ct)
    {
        // Phase B: collect every cross-pair link from the streamed file
        // into a per-canonical-pair group. Canonical = (lower_id,
        // higher_id) — both (a→b) and (b→a) hash to the same bucket so
        // we treat them as one logical pair downstream.
        // ~24 bytes per entry × ~1M cross-pair links for a typical
        // en-ru pair ≈ 24 MB — fine to materialise.
        var pairGroups = new Dictionary<(long Lo, long Hi), List<LinkRow>>();
        await foreach (var link in StreamLinksAsync(linksPath, ct))
        {
            var spans =
                (langIds.Contains(link.SourceId) && pairIds.Contains(link.TargetId))
                || (pairIds.Contains(link.SourceId) && langIds.Contains(link.TargetId));
            if (!spans)
                continue;

            var key = link.SourceId < link.TargetId
                ? (link.SourceId, link.TargetId)
                : (link.TargetId, link.SourceId);

            if (!pairGroups.TryGetValue(key, out var rows))
            {
                rows = new List<LinkRow>(2);
                pairGroups[key] = rows;
            }
            rows.Add(link);
        }

        // Phase C: sort canonical pairs by (lo, hi) for determinism,
        // then take pairs in order until the budget is hit, enforcing
        // the per-sentence pair cap so a single sentence can't dominate
        // the kept set.
        var sortedKeys = pairGroups.Keys
            .OrderBy(k => k.Lo)
            .ThenBy(k => k.Hi)
            .ToList();

        var keptLinks = new List<LinkRow>();
        var keptSentenceIds = new HashSet<long>();
        var pairsPerSentence = new Dictionary<long, int>();
        long keptPairCount = 0;

        foreach (var key in sortedKeys)
        {
            if (keptPairCount >= budget)
                break;

            var loCount = pairsPerSentence.GetValueOrDefault(key.Lo, 0);
            var hiCount = pairsPerSentence.GetValueOrDefault(key.Hi, 0);
            if (loCount >= MaxPairsPerSentence || hiCount >= MaxPairsPerSentence)
                continue;

            // Add ALL link rows for this canonical pair (typically both
            // directions, since Tatoeba's links file is bidirectional).
            keptLinks.AddRange(pairGroups[key]);
            keptSentenceIds.Add(key.Lo);
            keptSentenceIds.Add(key.Hi);
            pairsPerSentence[key.Lo] = loCount + 1;
            pairsPerSentence[key.Hi] = hiCount + 1;
            keptPairCount++;
        }

        return (keptLinks, keptSentenceIds, keptPairCount);
    }

    private async Task<HashSet<long>> CopySentencesAsync(
        NpgsqlConnection connection,
        string sentencesPath,
        string sentenceLangCode,
        HashSet<long>? filter,
        CancellationToken ct)
    {
        var ids = new HashSet<long>();

        await using var importer = await connection.BeginBinaryImportAsync(
            "COPY tatoeba_sentences (lang_code, sentence_id, text, source) FROM STDIN (FORMAT BINARY)",
            ct);

        await foreach (var row in StreamSentencesAsync(sentencesPath, ct))
        {
            // When filter is provided, only emit rows whose ID is in
            // the kept set. Used by the --max-links pre-filter flow to
            // skip COPYing sentences that won't appear in any kept link.
            if (filter is not null && !filter.Contains(row.Id))
                continue;

            await importer.StartRowAsync(ct);
            await importer.WriteAsync(sentenceLangCode, NpgsqlDbType.Varchar, ct);
            await importer.WriteAsync(row.Id, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(row.Text, NpgsqlDbType.Text, ct);
            await importer.WriteAsync(source, NpgsqlDbType.Varchar, ct);

            ids.Add(row.Id);
        }

        await importer.CompleteAsync(ct);

        return ids;
    }

    private async Task CopyKeptLinksAsync(
        NpgsqlConnection connection,
        IReadOnlyList<LinkRow> links,
        CancellationToken ct)
    {
        await using var importer = await connection.BeginBinaryImportAsync(
            "COPY tatoeba_links (source_id, target_id, source) FROM STDIN (FORMAT BINARY)",
            ct);

        foreach (var link in links)
        {
            await importer.StartRowAsync(ct);
            await importer.WriteAsync(link.SourceId, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(link.TargetId, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(source, NpgsqlDbType.Varchar, ct);
        }

        await importer.CompleteAsync(ct);
    }

    private async Task<long> CopyLinksAsync(
        NpgsqlConnection connection,
        string linksPath,
        HashSet<long> langIds,
        HashSet<long> pairIds,
        CancellationToken ct)
    {
        await using var importer = await connection.BeginBinaryImportAsync(
            "COPY tatoeba_links (source_id, target_id, source) FROM STDIN (FORMAT BINARY)",
            ct);

        long count = 0;
        await foreach (var link in StreamLinksAsync(linksPath, ct))
        {
            // Keep links whose endpoints span the two imported languages
            // (in either direction). Same-language links (e.g. paraphrases
            // within EN) and links touching unimported sentences are
            // dropped — they can't drive cross-language EntryContexts.
            var endpointsSpanPair =
                (langIds.Contains(link.SourceId) && pairIds.Contains(link.TargetId))
                || (pairIds.Contains(link.SourceId) && langIds.Contains(link.TargetId));

            if (!endpointsSpanPair)
                continue;

            await importer.StartRowAsync(ct);
            await importer.WriteAsync(link.SourceId, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(link.TargetId, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(source, NpgsqlDbType.Varchar, ct);
            count++;
        }

        await importer.CompleteAsync(ct);

        return count;
    }

    private async IAsyncEnumerable<SentenceRow> StreamSentencesAsync(
        string sentencesPath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(sentencesPath);

        Stream readStream = sentencesPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : fileStream;

        using var reader = new StreamReader(readStream);
        string? line;
        long lineNumber = 0;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;

            if (string.IsNullOrEmpty(line))
                continue;

            // Tatoeba per-language sentence files are id\tlang\ttext.
            // Use IndexOf instead of Split('\t', 3) so a literal tab inside
            // the text column (rare but possible — Tatoeba doesn't escape
            // it) lands in the text untouched.
            var firstTab = line.IndexOf('\t');
            var secondTab = firstTab < 0 ? -1 : line.IndexOf('\t', firstTab + 1);

            if (firstTab < 0 || secondTab < 0)
            {
                throw new InvalidOperationException(
                    $"Malformed TSV on line {lineNumber} of '{sentencesPath}': expected " +
                    $"3 tab-separated columns (id, lang, text). Line preview: {Preview(line)}.");
            }

            var idText = line[..firstTab];
            var text = line[(secondTab + 1)..];

            if (!long.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                || id <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid sentence id on line {lineNumber} of '{sentencesPath}': '{idText}'. " +
                    $"Expected a positive integer. Line preview: {Preview(line)}.");
            }

            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException(
                    $"Empty sentence text on line {lineNumber} of '{sentencesPath}'. " +
                    $"Line preview: {Preview(line)}.");
            }

            yield return new SentenceRow(id, text);
        }

        if (readStream != fileStream)
            await readStream.DisposeAsync();
    }

    private async IAsyncEnumerable<LinkRow> StreamLinksAsync(
        string linksPath,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(linksPath);

        Stream readStream = linksPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : fileStream;

        using var reader = new StreamReader(readStream);
        string? line;
        long lineNumber = 0;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;

            if (string.IsNullOrEmpty(line))
                continue;

            // Tatoeba's links.csv is `source_id\ttarget_id`. Two columns,
            // both integers.
            var parts = line.Split('\t');
            if (parts.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Malformed TSV on line {lineNumber} of '{linksPath}': expected 2 " +
                    $"tab-separated columns (source_id, target_id) but got {parts.Length}. " +
                    $"Line preview: {Preview(line)}.");
            }

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sourceId)
                || sourceId <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid source_id on line {lineNumber} of '{linksPath}': '{parts[0]}'. " +
                    $"Expected a positive integer. Line preview: {Preview(line)}.");
            }

            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetId)
                || targetId <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid target_id on line {lineNumber} of '{linksPath}': '{parts[1]}'. " +
                    $"Expected a positive integer. Line preview: {Preview(line)}.");
            }

            yield return new LinkRow(sourceId, targetId);
        }

        if (readStream != fileStream)
            await readStream.DisposeAsync();
    }

    private static string ResolveFile(string directoryPath, string baseName)
    {
        // Accept the file plain or .gz — the downloader writes plain after
        // decompressing Tatoeba's .bz2 archives, but a maintainer who
        // re-gzipped the dir for cold storage shouldn't have to undo that.
        var plain = Path.Combine(directoryPath, baseName);
        if (File.Exists(plain))
            return plain;

        var gzipped = Path.Combine(directoryPath, baseName + ".gz");
        if (File.Exists(gzipped))
            return gzipped;

        throw new FileNotFoundException(
            $"Required file not found in '{directoryPath}': expected " +
            $"'{baseName}' or '{baseName}.gz'. Run scripts/download-tatoeba.sh " +
            $"to populate the directory.",
            plain);
    }

    private static string FormatDuration(TimeSpan elapsed) =>
        elapsed.TotalSeconds < 1
            ? $"{elapsed.TotalMilliseconds:N0}ms"
            : elapsed.TotalSeconds < 60
                ? $"{elapsed.TotalSeconds:N1}s"
                : $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds:D2}s";

    private static string Preview(string line) =>
        line.Length > 200 ? line[..200] + "..." : line;

    private sealed record SentenceRow(long Id, string Text);

    private sealed record LinkRow(long SourceId, long TargetId);
}
