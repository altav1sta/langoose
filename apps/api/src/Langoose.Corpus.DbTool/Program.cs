using System.Diagnostics;
using Langoose.Corpus.DbTool;
using Langoose.Corpus.DbTool.Importers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

if (args.Length == 0)
{
    Console.Error.WriteLine(
        """
        Langoose corpus DbTool. Subcommands:

          init                              Apply embedded SQL schema to the corpus database.
          reset-wiktionary                  Drop every wiktionary_entries_<lang>
                                            partition (and its indexes) and clear
                                            all source_wiktionary_* metadata
                                            rows. Use at the start of a full bulk
                                            build so languages removed from the
                                            LANGUAGES list don't linger in the dump
                                            as empty partitions.
          reset-wordfreq                    Truncate wordfreq_rankings and clear all
                                            source_wordfreq_* metadata rows.
                                            Wordfreq counterpart of reset-wiktionary.
                                            Required at the start of a rebuild — the
                                            per-import (lang, source) DELETE alone
                                            doesn't catch rows from prior dates or
                                            languages dropped from LANGUAGES, which
                                            would pollute --frequency-filter-top and
                                            the published dump.
          import-wiktionary --lang <code>   Import a Kaikki Wiktionary JSONL extract.
                            --source <path>
                            [--source-version <ver>]
                            [--limit <n>]   Stop after <n> imported entries (mini dump).
                            [--frequency-filter-top <n>]
                                            Skip entries whose headword isn't in the
                                            top <n> of wordfreq_rankings for this
                                            language. Requires `import-wordfreq` to
                                            have run first. Used by mini-dump builds
                                            instead of --limit so the snapshot is
                                            representative of everyday vocabulary.
                            [--defer-indexes]
                                            Skip the post-COPY index rebuild. Use when
                                            importing multiple languages in sequence;
                                            follow with `rebuild-indexes` at the end.
          import-wordfreq   --lang <code>   Import a wordfreq frequency-ranking TSV
                            --source <path> (word\trank\tzipf_score, gz allowed).
                            [--source-version <ver>]
                                            Defaults to wordfreq-<UTC date>. Stored in
                                            the `source` column so multiple frequency
                                            sources (wordfreq, SUBTLEX, ...) can
                                            coexist for the same language.
          rebuild-indexes                   For every wiktionary_entries_<lang>
                                            partition that exists, drop and
                                            recreate its two indexes. Idempotent.
                                            Typically the final step of a bulk
                                            multi-language build that ran each
                                            import-wiktionary with --defer-indexes.
        """);

    return 1;
}

var command = args[0];
var commandArgs = args[1..];

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = commandArgs,
    ContentRootPath = AppContext.BaseDirectory
});
var connectionString = builder.Configuration.GetConnectionString("CorpusDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'CorpusDatabase' is not configured.");

await using var dataSource = NpgsqlDataSource.Create(connectionString);

return command switch
{
    "init" => await RunInitAsync(dataSource),
    "reset-wiktionary" => await RunResetWiktionaryAsync(dataSource),
    "reset-wordfreq" => await RunResetWordfreqAsync(dataSource),
    "import-wiktionary" => await RunImportWiktionaryAsync(dataSource, commandArgs),
    "import-wordfreq" => await RunImportWordfreqAsync(dataSource, commandArgs),
    "rebuild-indexes" => await RunRebuildIndexesAsync(dataSource),
    _ => UnknownCommand(command)
};

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown subcommand: {command}");
    return 1;
}

static async Task<int> RunInitAsync(NpgsqlDataSource dataSource)
{
    var initializer = new CorpusInitializer(dataSource);
    await initializer.ApplySchemaAsync();

    Console.WriteLine("Corpus schema applied.");

    return 0;
}

static async Task<int> RunResetWiktionaryAsync(NpgsqlDataSource dataSource)
{
    var stopwatch = Stopwatch.StartNew();
    Console.WriteLine("Resetting wiktionary data (drop all partitions + clear metadata)...");

    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    // Per #97 wiktionary_entries is LIST-partitioned by lang_code. Dropping
    // every partition (rather than just truncating the parent) makes the
    // dump match LANGUAGES exactly — a partition for a language no longer
    // in LANGUAGES would otherwise linger as an empty partition in the
    // dump. The importer's `EnsurePartitionAsync` recreates them on demand.
    var partitions = await WiktionaryIndexMaintenance.ListPartitionLangCodesAsync(
        connection, transaction, default);
    foreach (var lang in partitions)
    {
        await WiktionaryIndexMaintenance.DropPartitionAsync(
            connection, transaction, lang, default);
    }

    await using (var command = connection.CreateCommand())
    {
        command.Transaction = transaction;
        command.CommandTimeout = 0;
        command.CommandText = """
            DELETE FROM corpus_metadata
                WHERE key LIKE 'source_wiktionary_%';
            """;
        await command.ExecuteNonQueryAsync();
    }

    await transaction.CommitAsync();

    Console.WriteLine(
        $"Done in {FormatDuration(stopwatch.Elapsed)}. Dropped {partitions.Count} partition(s).");
    return 0;
}

static async Task<int> RunResetWordfreqAsync(NpgsqlDataSource dataSource)
{
    var stopwatch = Stopwatch.StartNew();
    Console.WriteLine("Resetting wordfreq data (truncate + clear metadata)...");

    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    await using (var command = connection.CreateCommand())
    {
        command.Transaction = transaction;
        command.CommandTimeout = 0;
        // import-wordfreq deletes per-(lang_code, source) before its
        // COPY, so a same-day re-import is already idempotent. This
        // reset exists for the "rebuild" case: cross-date rebuilds
        // would otherwise accumulate one (lang, wordfreq-<old-date>)
        // row set per run, and dropping a language from LANGUAGES
        // would leave its prior rows behind. Both would silently
        // pollute --frequency-filter-top (which unions ranks across
        // all sources for a language) and the published dump.
        // Wordfreq's only index (lang_code, rank) is cheap to maintain
        // during COPY, so we don't bother dropping it here.
        command.CommandText = """
            TRUNCATE TABLE wordfreq_rankings;
            DELETE FROM corpus_metadata
                WHERE key LIKE 'source_wordfreq_%';
            """;
        await command.ExecuteNonQueryAsync();
    }

    await transaction.CommitAsync();

    Console.WriteLine($"Done in {FormatDuration(stopwatch.Elapsed)}.");
    return 0;
}

static async Task<int> RunImportWiktionaryAsync(NpgsqlDataSource dataSource, string[] commandArgs)
{
    var langCode = GetRequiredOption(commandArgs, "--lang");
    var sourcePath = GetRequiredOption(commandArgs, "--source");
    var source = GetOption(commandArgs, "--source-version")
        ?? $"wiktionary-{DateTime.UtcNow:yyyy-MM-dd}";
    var limit = GetOption(commandArgs, "--limit") is { } limitText
        ? long.Parse(limitText)
        : (long?)null;
    var frequencyFilterTop = GetOption(commandArgs, "--frequency-filter-top") is { } topText
        ? int.Parse(topText)
        : (int?)null;
    var deferIndexes = HasFlag(commandArgs, "--defer-indexes");

    var importer = new WiktionaryImporter(
        dataSource, langCode, source, limit, deferIndexes, frequencyFilterTop);
    var summary = await importer.ImportAsync(sourcePath);

    Console.WriteLine(
        $"""
        Imported {summary.EntriesImported} entries from {summary.Source} (source {summary.SourceVersion}).
        """);

    return 0;
}

static async Task<int> RunImportWordfreqAsync(NpgsqlDataSource dataSource, string[] commandArgs)
{
    var langCode = GetRequiredOption(commandArgs, "--lang");
    var sourcePath = GetRequiredOption(commandArgs, "--source");
    var source = GetOption(commandArgs, "--source-version")
        ?? $"wordfreq-{DateTime.UtcNow:yyyy-MM-dd}";

    var importer = new WordfreqImporter(dataSource, langCode, source);
    var summary = await importer.ImportAsync(sourcePath);

    Console.WriteLine(
        $"""
        Imported {summary.EntriesImported} rankings from {summary.Source} (source {summary.SourceVersion}).
        """);

    return 0;
}

static async Task<int> RunRebuildIndexesAsync(NpgsqlDataSource dataSource)
{
    var totalStopwatch = Stopwatch.StartNew();
    Console.WriteLine("Rebuilding wiktionary_entries partition indexes...");

    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    // Iterate every partition that exists in this database. Per-partition
    // drop+create keeps each language's indexes named distinctly
    // (ix_wiktionary_entries_<lang>_lookup / _data) so they don't collide
    // and so a future single-language re-import can rebuild just one set.
    var partitions = await WiktionaryIndexMaintenance.ListPartitionLangCodesAsync(
        connection, transaction, default);
    if (partitions.Count == 0)
    {
        Console.WriteLine(
            "  no partitions found — nothing to rebuild. Did you skip the import step?");
    }

    foreach (var lang in partitions)
    {
        var dropElapsed = await WiktionaryIndexMaintenance.DropAsync(
            connection, transaction, lang, default);
        var createElapsed = await WiktionaryIndexMaintenance.CreateAsync(
            connection, transaction, lang, default);
        Console.WriteLine(
            $"  {lang}: dropped in {FormatDuration(dropElapsed)}, built in {FormatDuration(createElapsed)}");
    }

    await transaction.CommitAsync();

    Console.WriteLine($"Done in {FormatDuration(totalStopwatch.Elapsed)}.");

    return 0;
}

static string FormatDuration(TimeSpan elapsed) =>
    elapsed.TotalSeconds < 1
        ? $"{elapsed.TotalMilliseconds:N0}ms"
        : elapsed.TotalSeconds < 60
            ? $"{elapsed.TotalSeconds:N1}s"
            : $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds:D2}s";

static string GetRequiredOption(string[] args, string name) =>
    GetOption(args, name)
        ?? throw new ArgumentException($"Missing required option: {name}");

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name)
            return args[i + 1];
    }

    return null;
}

static bool HasFlag(string[] args, string name) =>
    args.Any(arg => arg == name);
