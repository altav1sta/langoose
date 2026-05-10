using System.Text.RegularExpressions;
using Npgsql;

namespace Langoose.Corpus.DbTool;

/// <summary>
/// Per-partition operations for <c>tatoeba_sentences</c>. The DDL templates
/// themselves live in <c>004_tatoeba.sql</c> as PL/pgSQL helpers (see
/// <c>corpus_tatoeba_*</c> functions); this class is a thin C# RPC layer
/// that passes <c>lang_code</c> as a parameter.
///
/// Mirrors <see cref="WiktionaryIndexMaintenance"/> but stops at
/// ensure/drop/truncate/list — Tatoeba has no separate user indexes to
/// drop/recreate around COPY (the per-partition PK index is sufficient
/// and BTree maintenance during COPY is cheap).
/// </summary>
public static class TatoebaIndexMaintenance
{
    private static readonly Regex LangCodePattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public static string PartitionName(string langCode)
    {
        ValidateLangCode(langCode);
        return "tatoeba_sentences_" + langCode;
    }

    public static async Task EnsurePartitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);
        await CallVoidFunctionAsync(
            connection, transaction, "corpus_tatoeba_ensure_partition", langCode, ct);
    }

    public static async Task DropPartitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);
        await CallVoidFunctionAsync(
            connection, transaction, "corpus_tatoeba_drop_partition", langCode, ct);
    }

    public static async Task TruncatePartitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string langCode,
        CancellationToken ct)
    {
        ValidateLangCode(langCode);
        await CallVoidFunctionAsync(
            connection, transaction, "corpus_tatoeba_truncate_partition", langCode, ct);
    }

    public static async Task<IReadOnlyList<string>> ListPartitionLangCodesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM corpus_tatoeba_list_partition_lang_codes()";

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
