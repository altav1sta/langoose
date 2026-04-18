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
          reset-wiktionary                  Truncate wiktionary_entries and clear all
                                            source_version_wiktionary_* metadata rows.
                                            Use at the start of a full bulk build so
                                            languages removed from the LANGUAGES list
                                            don't linger in the dump.
          import-wiktionary --lang <code>   Import a Kaikki Wiktionary JSONL extract.
                            --source <path>
                            [--source-version <ver>]
                            [--limit <n>]   Stop after <n> imported entries (mini dump).
                            [--defer-indexes]
                                            Skip the post-COPY index rebuild. Use when
                                            importing multiple languages in sequence;
                                            follow with `rebuild-indexes` at the end.
          rebuild-indexes                   Drop and recreate the wiktionary_entries
                                            indexes. Idempotent. Typically the final
                                            step of a bulk multi-language build.
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
    "import-wiktionary" => await RunImportWiktionaryAsync(dataSource, commandArgs),
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
    Console.WriteLine("Resetting wiktionary data (truncate + clear metadata + drop indexes)...");

    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    await using (var command = connection.CreateCommand())
    {
        command.Transaction = transaction;
        command.CommandTimeout = 0;
        // TRUNCATE avoids the per-row scan cost of a big DELETE and
        // reclaims space immediately. CASCADE isn't needed — no FKs target
        // this table.
        command.CommandText = """
            TRUNCATE TABLE wiktionary_entries;
            DELETE FROM corpus_metadata
                WHERE key LIKE 'source_version_wiktionary_%';
            """;
        await command.ExecuteNonQueryAsync();
    }

    // Dropping the indexes here (as part of the reset) means subsequent
    // `import-wiktionary --defer-indexes` calls don't need to repeat the
    // DROP IF EXISTS dance — they can assume a fresh, unindexed table.
    // `rebuild-indexes` at the end of the bulk flow recreates them.
    await WiktionaryIndexMaintenance.DropAsync(connection, transaction, default);

    await transaction.CommitAsync();

    Console.WriteLine($"Done in {FormatDuration(stopwatch.Elapsed)}.");
    return 0;
}

static async Task<int> RunImportWiktionaryAsync(NpgsqlDataSource dataSource, string[] commandArgs)
{
    var langCode = GetRequiredOption(commandArgs, "--lang");
    var sourcePath = GetRequiredOption(commandArgs, "--source");
    var sourceVersion = GetOption(commandArgs, "--source-version")
        ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
    var limit = GetOption(commandArgs, "--limit") is { } limitText
        ? long.Parse(limitText)
        : (long?)null;
    var deferIndexes = HasFlag(commandArgs, "--defer-indexes");

    var importer = new WiktionaryImporter(
        dataSource, langCode, sourceVersion, limit, deferIndexes);
    var summary = await importer.ImportAsync(sourcePath);

    Console.WriteLine(
        $"""
        Imported {summary.EntriesImported} entries from {summary.Source} (version {summary.SourceVersion}).
        """);

    return 0;
}

static async Task<int> RunRebuildIndexesAsync(NpgsqlDataSource dataSource)
{
    var totalStopwatch = Stopwatch.StartNew();
    Console.WriteLine("Rebuilding wiktionary_entries indexes...");

    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    var dropElapsed = await WiktionaryIndexMaintenance.DropAsync(connection, transaction, default);
    Console.WriteLine($"  dropped existing indexes in {FormatDuration(dropElapsed)}");

    var createElapsed = await WiktionaryIndexMaintenance.CreateAsync(connection, transaction, default);
    Console.WriteLine($"  built indexes in {FormatDuration(createElapsed)}");

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
