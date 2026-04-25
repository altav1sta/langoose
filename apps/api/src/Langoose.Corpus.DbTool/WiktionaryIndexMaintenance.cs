using System.Diagnostics;
using System.Text.RegularExpressions;
using Npgsql;

namespace Langoose.Corpus.DbTool;

/// <summary>
/// Per-partition operations for <c>wiktionary_entries</c>. The DDL
/// templates themselves live in <c>002_wiktionary.sql</c> as PL/pgSQL
/// helpers (see <c>corpus_wiktionary_*</c> functions); this class is a
/// thin C# RPC layer that passes <c>lang_code</c> as a parameter and
/// times the call.
/// </summary>
public static class WiktionaryIndexMaintenance
{
    // Matches the regex inside corpus_wiktionary_assert_lang_code() in
    // 002_wiktionary.sql. Duplicated as a fast fail-fast check so callers
    // get a clean ArgumentException before opening a connection; the SQL
    // function is the authoritative gate (it also runs when these helpers
    // are invoked from psql).
    private static readonly Regex LangCodePattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public static string PartitionName(string langCode)
    {
        ValidateLangCode(langCode);
        return "wiktionary_entries_" + langCode;
    }

    public static async Task EnsurePartitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);
        await CallVoidFunctionAsync(
            connection, transaction, "corpus_wiktionary_ensure_partition", langCode, ct);
    }

    public static async Task DropPartitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);
        await CallVoidFunctionAsync(
            connection, transaction, "corpus_wiktionary_drop_partition", langCode, ct);
    }

    public static async Task TruncatePartitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);
        await CallVoidFunctionAsync(
            connection, transaction, "corpus_wiktionary_truncate_partition", langCode, ct);
    }

    public static async Task<TimeSpan> DropAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);

        var stopwatch = Stopwatch.StartNew();
        await CallVoidFunctionAsync(
            connection, transaction, "corpus_wiktionary_drop_partition_indexes", langCode, ct);
        return stopwatch.Elapsed;
    }

    public static async Task<TimeSpan> CreateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);

        var stopwatch = Stopwatch.StartNew();

        // SET LOCAL has to live at the transaction level — issuing it
        // inside the SQL function only affects the function's call
        // frame, not the surrounding CREATE INDEX. maintenance_work_mem
        // defaults to 64 MB, which forces GIN's bulk build to spill
        // posting-list sorts to disk; 1 GB plus 4 parallel workers
        // keeps the merge in memory and is calibrated for Docker
        // Desktop defaults (~4-8 GB container memory). Reduce if your
        // Postgres container has significantly less.
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SET LOCAL maintenance_work_mem = '1GB';
            SET LOCAL max_parallel_maintenance_workers = 4;
            SELECT corpus_wiktionary_create_partition_indexes(@lang_code);
            """;
        command.Parameters.AddWithValue("lang_code", langCode);
        command.CommandTimeout = 0;

        await command.ExecuteNonQueryAsync(ct);

        return stopwatch.Elapsed;
    }

    public static async Task<IReadOnlyList<string>> ListPartitionLangCodesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM corpus_wiktionary_list_partition_lang_codes()";

        var langCodes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            langCodes.Add(reader.GetString(0));

        return langCodes;
    }

    private static async Task CallVoidFunctionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string functionName,
        string langCode,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {functionName}(@lang_code)";
        command.Parameters.AddWithValue("lang_code", langCode);
        command.CommandTimeout = 0;

        await command.ExecuteNonQueryAsync(ct);
    }

    private static void ValidateLangCode(string langCode)
    {
        if (string.IsNullOrEmpty(langCode) || !LangCodePattern.IsMatch(langCode))
        {
            throw new ArgumentException(
                $"Invalid lang_code '{langCode}': must match [a-z][a-z0-9_]*.",
                nameof(langCode));
        }
    }
}
