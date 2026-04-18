using System.Diagnostics;
using Npgsql;

namespace Langoose.Corpus.DbTool;

/// <summary>
/// Manages the drop/rebuild of the wiktionary_entries indexes. Kept
/// separate from <c>WiktionaryImporter</c> so it can be reused by the
/// standalone <c>rebuild-indexes</c> command (for finishing a bulk
/// multi-language build after all per-lang COPYs have run with
/// <c>--defer-indexes</c>).
/// </summary>
public static class WiktionaryIndexMaintenance
{
    private const string DropIndexesSql =
        """
        DROP INDEX IF EXISTS ix_wiktionary_entries_lookup;
        DROP INDEX IF EXISTS ix_wiktionary_entries_data;
        """;

    // maintenance_work_mem defaults to 64MB, which forces GIN's bulk build
    // to spill posting-list sorts to disk for any non-trivial corpus — and
    // disk spills on Docker Desktop Windows are very slow. SET LOCAL gives
    // the build a large in-memory budget just for this transaction;
    // parallel workers further amortise the posting-list merge. Values
    // tuned for Docker Desktop defaults (typically 4-8 GB container
    // memory); reduce if your Postgres container is more constrained.
    private const string CreateIndexesSql =
        """
        SET LOCAL maintenance_work_mem = '1GB';
        SET LOCAL max_parallel_maintenance_workers = 4;

        CREATE INDEX ix_wiktionary_entries_lookup
            ON wiktionary_entries (lang_code, word, pos);
        CREATE INDEX ix_wiktionary_entries_data
            ON wiktionary_entries USING GIN (data jsonb_path_ops);
        """;

    public static async Task<TimeSpan> DropAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = DropIndexesSql;
        command.CommandTimeout = 0;

        await command.ExecuteNonQueryAsync(ct);

        return stopwatch.Elapsed;
    }

    public static async Task<TimeSpan> CreateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = CreateIndexesSql;
        command.CommandTimeout = 0;

        await command.ExecuteNonQueryAsync(ct);

        return stopwatch.Elapsed;
    }
}
