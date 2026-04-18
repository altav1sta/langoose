using Dapper;
using FluentAssertions;
using Langoose.Corpus.DbTool;
using Langoose.Corpus.IntegrationTests.Infrastructure;
using Xunit;

namespace Langoose.Corpus.IntegrationTests;

public sealed class CorpusInitializerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ApplySchemaAsync_OnFreshDatabase_CreatesAllExpectedTables()
    {
        var initializer = new CorpusInitializer(postgres.DataSource);

        await initializer.ApplySchemaAsync();

        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        var tableNames = (await connection.QueryAsync<string>(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            """)).ToHashSet();

        tableNames.Should().Contain("corpus_metadata");
        tableNames.Should().Contain("wiktionary_entries");
    }

    [Fact]
    public async Task ApplySchemaAsync_RunTwice_IsIdempotent()
    {
        var initializer = new CorpusInitializer(postgres.DataSource);

        await initializer.ApplySchemaAsync();
        await initializer.ApplySchemaAsync();

        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        var tableCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('corpus_metadata', 'wiktionary_entries')
            """);

        tableCount.Should().Be(2);
    }
}
