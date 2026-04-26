using System.Runtime.CompilerServices;
using Langoose.Corpus.Data.Models;
using Npgsql;
using NpgsqlTypes;

namespace Langoose.Corpus.Data.Readers;

/// <summary>
/// Streams ranked Wiktionary entries from the corpus database for the
/// bulk-seed pipeline, grouped into (language, word, pos) bundles. Rows
/// share a bundle when Wiktionary published etymology splits under the
/// same headword and POS.
///
/// The reader pins to a wiktionary <c>source</c> and a wordfreq
/// <c>source</c>; the worker validates these still exist via
/// <see cref="SnapshotsExistAsync"/> before each batch so a re-import
/// during a long-running job surfaces as a Failed job rather than as
/// silently rotating data underneath the cursor.
/// </summary>
public sealed class BulkImportCorpusReader(NpgsqlDataSource dataSource) : IBulkImportCorpusReader
{
    public async Task<bool> SnapshotsExistAsync(
        string language,
        string wiktionarySource,
        string wordfreqSource,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                EXISTS (SELECT 1 FROM wiktionary_entries
                        WHERE lang_code = @lang AND source = @wikt_source
                        LIMIT 1) AS wikt_exists,
                EXISTS (SELECT 1 FROM wordfreq_rankings
                        WHERE lang_code = @lang AND source = @wf_source
                        LIMIT 1) AS wf_exists
            """;
        command.Parameters.AddWithValue("lang", language);
        command.Parameters.AddWithValue("wikt_source", wiktionarySource);
        command.Parameters.AddWithValue("wf_source", wordfreqSource);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return false;

        return reader.GetBoolean(0) && reader.GetBoolean(1);
    }

    public async IAsyncEnumerable<WiktionaryBundle> StreamBundlesAsync(
        BulkImportCorpusQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = """
            SELECT
                w.lang_code,
                w.word,
                w.pos,
                w.source,
                w.data::text AS data_json,
                r.rank
            FROM wiktionary_entries w
            JOIN wordfreq_rankings r
                ON r.lang_code = w.lang_code AND r.word = w.word
            WHERE w.lang_code = @lang
              AND w.source = @wikt_source
              AND r.source = @wf_source
              AND (@top_rank IS NULL OR r.rank <= @top_rank)
              AND (
                @last_rank IS NULL OR
                (r.rank, w.word, w.pos) > (@last_rank, @last_word, @last_pos)
              )
            ORDER BY r.rank, w.word, w.pos
            """;
        command.Parameters.Add(new NpgsqlParameter("lang", NpgsqlDbType.Text) { Value = query.Language });
        command.Parameters.Add(new NpgsqlParameter("wikt_source", NpgsqlDbType.Text) { Value = query.WiktionarySource });
        command.Parameters.Add(new NpgsqlParameter("wf_source", NpgsqlDbType.Text) { Value = query.WordfreqSource });
        command.Parameters.Add(new NpgsqlParameter("top_rank", NpgsqlDbType.Integer) { Value = (object?)query.TopFrequencyRank ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("last_rank", NpgsqlDbType.Integer) { Value = (object?)query.Cursor_LastRank ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("last_word", NpgsqlDbType.Text) { Value = (object?)query.Cursor_LastWord ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("last_pos", NpgsqlDbType.Text) { Value = (object?)query.Cursor_LastPos ?? DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync(ct);

        var pendingRows = new List<WiktionaryEntryRow>();
        var pendingRank = 0;
        string? pendingWord = null;
        string? pendingPos = null;
        string? pendingLang = null;

        while (await reader.ReadAsync(ct))
        {
            var langCode = reader.GetString(0);
            var word = reader.GetString(1);
            var pos = reader.GetString(2);
            var source = reader.GetString(3);
            var dataJson = reader.GetString(4);
            var rank = reader.GetInt32(5);

            var row = new WiktionaryEntryRow(langCode, word, pos, source, dataJson);

            var groupChanged = pendingWord is not null
                && (pendingWord != word || pendingPos != pos);

            if (groupChanged)
            {
                yield return new WiktionaryBundle(
                    pendingRank, pendingLang!, pendingWord!, pendingPos!, pendingRows.ToArray());
                pendingRows.Clear();
            }

            pendingRows.Add(row);
            pendingRank = rank;
            pendingLang = langCode;
            pendingWord = word;
            pendingPos = pos;
        }

        if (pendingRows.Count > 0)
        {
            yield return new WiktionaryBundle(
                pendingRank, pendingLang!, pendingWord!, pendingPos!, pendingRows.ToArray());
        }
    }
}
