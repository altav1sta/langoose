using Dapper;
using FluentAssertions;
using Langoose.Corpus.DbTool;
using Langoose.Corpus.IntegrationTests.Infrastructure;
using Xunit;

namespace Langoose.Corpus.IntegrationTests;

public sealed class CorpusInitializerTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Each test starts on a fresh public schema. The container is
        // shared across the class via PostgresFixture, but tests in this
        // file assert on first-init state (e.g. that a fresh DB lands
        // wiktionary_entries as a partitioned table). Recreating the
        // schema is cheap relative to the container start.
        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
    public async Task ApplySchemaAsync_OnFreshDatabase_CreatesWiktionaryEntriesAsPartitionedTable()
    {
        var initializer = new CorpusInitializer(postgres.DataSource);

        await initializer.ApplySchemaAsync();

        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        // pg_class.relkind 'p' indicates a partitioned table. If
        // 002_wiktionary.sql lost the PARTITION BY clause this test fails.
        var relkind = await connection.QuerySingleAsync<char>(
            """
            SELECT c.relkind
            FROM pg_class c
            JOIN pg_namespace ns ON ns.oid = c.relnamespace
            WHERE c.relname = 'wiktionary_entries' AND ns.nspname = 'public'
            """);

        relkind.Should().Be('p');
    }

    [Fact]
    public async Task ApplySchemaAsync_OnFreshDatabase_RegistersWiktionaryHelperFunctions()
    {
        var initializer = new CorpusInitializer(postgres.DataSource);

        await initializer.ApplySchemaAsync();

        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        var functions = (await connection.QueryAsync<string>(
            """
            SELECT proname
            FROM pg_proc p
            JOIN pg_namespace ns ON ns.oid = p.pronamespace
            WHERE ns.nspname = 'public'
              AND proname LIKE 'corpus_wiktionary_%'
            ORDER BY proname
            """)).ToArray();

        // Lock the helper surface — these names are part of the contract
        // between 002_wiktionary.sql and WiktionaryIndexMaintenance.cs.
        functions.Should().Equal(
            "corpus_wiktionary_assert_lang_code",
            "corpus_wiktionary_create_partition_indexes",
            "corpus_wiktionary_drop_partition",
            "corpus_wiktionary_drop_partition_indexes",
            "corpus_wiktionary_ensure_partition",
            "corpus_wiktionary_list_partition_lang_codes",
            "corpus_wiktionary_truncate_partition");
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
