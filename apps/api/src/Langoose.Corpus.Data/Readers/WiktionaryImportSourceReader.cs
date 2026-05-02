using System.Text.Json;
using Langoose.Domain.Imports;
using Langoose.Domain.Models;
using Npgsql;
using NpgsqlTypes;

namespace Langoose.Corpus.Data.Readers;

/// <summary>
/// <see cref="IImportSourceReader"/> for Kaikki Wiktionary data. Streams
/// entries pinned to a wiktionary <c>source</c>, groups etymology splits
/// into a single <see cref="ImportPayload"/> per (language, word, pos),
/// orders by (word, pos) for stable cursor advancement.
///
/// Frequency ranking is NOT applied here — the import ingests every
/// entry the source published. Frequency-driven prioritisation belongs
/// to query-time consumers (study session, dashboard) on top of a
/// frequency mapping that is populated separately (out of scope for
/// #105).
/// </summary>
public sealed class WiktionaryImportSourceReader(NpgsqlDataSource dataSource) : IImportSourceReader
{
    public string SourceName => "wiktionary";

    public async Task<bool> SnapshotExistsAsync(string language, string snapshot, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM wiktionary_entries
                WHERE lang_code = @lang AND source = @snapshot
                LIMIT 1)
            """;
        command.Parameters.AddWithValue("lang", language);
        command.Parameters.AddWithValue("snapshot", snapshot);

        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<ImportPayload[]> FetchBatchAsync(
        string language,
        string snapshot,
        int batchSize,
        string? cursor,
        CancellationToken ct)
    {
        var (lastWord, lastPos) = DecodeCursor(cursor);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        // GROUP BY (word, pos) collapses wiktionary's etymology-split rows
        // into one bundle per call; LIMIT then applies at the bundle
        // level, so a batch never cuts an etymology group in half. The
        // index on (lang_code, word, pos) drives an ordered index scan —
        // the ORDER BY is essentially free.
        command.CommandText = """
            SELECT
                w.word,
                w.pos,
                array_agg(w.data::text ORDER BY w.data->>'etymology_number' NULLS LAST) AS data_jsons
            FROM wiktionary_entries w
            WHERE w.lang_code = @lang
              AND w.source = @snapshot
              AND (
                @last_word IS NULL OR
                (w.word, w.pos) > (@last_word, @last_pos)
              )
            GROUP BY w.word, w.pos
            ORDER BY w.word, w.pos
            LIMIT @batch_size
            """;
        command.Parameters.Add(new NpgsqlParameter("lang", NpgsqlDbType.Text) { Value = language });
        command.Parameters.Add(new NpgsqlParameter("snapshot", NpgsqlDbType.Text) { Value = snapshot });
        command.Parameters.Add(new NpgsqlParameter("last_word", NpgsqlDbType.Text) { Value = (object?)lastWord ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("last_pos", NpgsqlDbType.Text) { Value = (object?)lastPos ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("batch_size", NpgsqlDbType.Integer) { Value = batchSize });

        var payloads = new List<ImportPayload>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var word = reader.GetString(0);
                var pos = reader.GetString(1);
                var dataJsons = (string[])reader.GetValue(2);
                payloads.Add(BuildPayload(language, word, pos, dataJsons));
            }
        }

        return payloads.ToArray();
    }

    private static ImportPayload BuildPayload(
        string language, string word, string pos, IReadOnlyList<string> rowJsons)
    {
        var senses = new List<ImportPayloadSense>();
        var senseIndex = 0;

        foreach (var rowJson in rowJsons)
        {
            var entry = JsonSerializer.Deserialize(rowJson, CorpusJsonContext.Default.WiktionaryEntry);
            if (entry?.Senses is null)
                continue;

            foreach (var sense in entry.Senses)
            {
                var translations = (sense.Translations ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x.Code) && !string.IsNullOrWhiteSpace(x.Word))
                    .Select(x => new ImportPayloadTranslation(x.Code!, x.Word!, null))
                    .ToArray();
                var gloss = sense.Glosses?.FirstOrDefault();

                senses.Add(new ImportPayloadSense(senseIndex++, gloss, translations));
            }
        }

        return new ImportPayload(
            new ImportPayloadEntry(language, word, pos),
            senses.ToArray());
    }

    private static (string? Word, string? Pos) DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return (null, null);

        var parts = cursor.Split(CursorDelimiter, 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (null, null);
    }

    public string EncodeCursorAfter(ImportPayload last) =>
        $"{last.Entry.Text}{CursorDelimiter}{last.Entry.Pos}";

    private const char CursorDelimiter = '\t';
}
