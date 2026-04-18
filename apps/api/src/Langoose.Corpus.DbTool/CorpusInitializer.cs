using Langoose.Corpus.Data.Schema;
using Npgsql;

namespace Langoose.Corpus.DbTool;

/// <summary>
/// Applies the corpus schema (embedded SQL files) to a target database in
/// a single transaction. Each script must be idempotent (uses IF NOT EXISTS).
/// </summary>
public sealed class CorpusInitializer(NpgsqlDataSource dataSource)
{
    public async Task ApplySchemaAsync(CancellationToken ct = default)
    {
        var scripts = CorpusSchema.GetSchemaScripts();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        foreach (var script in scripts)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = script.Sql;

            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
