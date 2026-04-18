using Dapper;
using FluentAssertions;
using Langoose.Corpus.DbTool;
using Langoose.Corpus.DbTool.Importers;
using Langoose.Corpus.IntegrationTests.Infrastructure;
using Xunit;

namespace Langoose.Corpus.IntegrationTests;

public sealed class WiktionaryImporterTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string FixtureEnPath = "fixtures/wiktionary-en-sample.jsonl";
    private const string FixtureRuPath = "fixtures/wiktionary-ru-sample.jsonl";

    public async Task InitializeAsync()
    {
        var initializer = new CorpusInitializer(postgres.DataSource);

        await initializer.ApplySchemaAsync();

        // Reset to a clean slate before each test so ordering doesn't
        // matter and future tests can't be broken by stale data. The
        // container itself is still reused across the class (disposable
        // per class via PostgresFixture) — truncating is cheaper than
        // booting a container per test.
        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE TABLE wiktionary_entries;
            DELETE FROM corpus_metadata;
            """;
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ImportAsync_FromEnglishFixture_LoadsAllEntriesPreservingRawPos()
    {
        var importer = new WiktionaryImporter(postgres.DataSource, "en", "test-1");

        var summary = await importer.ImportAsync(FixtureEnPath);

        summary.Source.Should().Be("wiktionary_en");
        summary.SourceVersion.Should().Be("test-1");
        // All 6 fixture entries are imported as-is (proper noun "London"
        // included; "lead" appears twice — once per etymology). No POS
        // filtering and no (lang, word, pos) collapsing at import time —
        // preserves the source.
        summary.EntriesImported.Should().Be(6);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var entries = (await connection.QueryAsync<(string lang, string word, string pos)>(
            "SELECT lang_code, word, pos FROM wiktionary_entries WHERE lang_code = 'en' ORDER BY word")).ToArray();

        entries.Should().BeEquivalentTo(new[]
        {
            ("en", "London", "name"),
            ("en", "book", "noun"),
            ("en", "good", "adj"),
            ("en", "lead", "noun"),
            ("en", "lead", "noun"),
            ("en", "run", "verb")
        });
    }

    [Fact]
    public async Task ImportAsync_PreservesEtymologySplitsAsSeparateRows()
    {
        var importer = new WiktionaryImporter(postgres.DataSource, "en", "test-1");

        await importer.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        // Kaikki/Wiktionary emits two "lead" (noun) entries — one per
        // etymology (metal vs. leash). Both must land as separate rows so
        // their distinct senses[] and translations[] remain accessible.
        // The absence of UNIQUE/PK on (lang_code, word, pos, source_version)
        // is what makes this work; adding one back would break this test.
        var etymologyNumbers = (await connection.QueryAsync<int>(
            """
            SELECT (data ->> 'etymology_number')::int AS etymology_number
            FROM wiktionary_entries
            WHERE lang_code = 'en' AND word = 'lead' AND pos = 'noun'
            ORDER BY etymology_number
            """)).ToArray();

        etymologyNumbers.Should().Equal(1, 2);
    }

    [Fact]
    public async Task ImportAsync_SupportsFormLookupViaJsonbContainment()
    {
        var importer = new WiktionaryImporter(postgres.DataSource, "ru", "test-1");

        await importer.ImportAsync(FixtureRuPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        // The intended runtime pattern: resolve "бронировал" (past masc sg)
        // to its lemma via JSONB containment against data->'forms'. Uses
        // the GIN index on data (jsonb_path_ops). No materialised form
        // index table — if this query ever shows up as a bottleneck, a
        // dedicated form-index table can be built as a follow-up.
        var lemma = await connection.QuerySingleOrDefaultAsync<string>(
            """
            SELECT word FROM wiktionary_entries
            WHERE lang_code = 'ru'
              AND data @> '{"forms":[{"form":"бронировал"}]}'::jsonb
            """);

        lemma.Should().Be("бронировать");
    }

    [Fact]
    public async Task ImportAsync_PreservesJsonbDataForTranslationLookup()
    {
        var importer = new WiktionaryImporter(postgres.DataSource, "en", "test-1");

        await importer.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        // Verify the JSONB containment query path works: "is 'книга' a
        // translation of 'book' (en/noun)?" — the same query the future
        // provider will use.
        var isTranslation = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM wiktionary_entries
                WHERE lang_code = 'en' AND word = 'book' AND pos = 'noun'
                  AND data @> '{"senses": [{"translations": [{"code": "ru", "word": "книга"}]}]}'::jsonb
            )
            """);

        isTranslation.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_RunTwice_ReplacesPreviousRows()
    {
        var importer = new WiktionaryImporter(postgres.DataSource, "en", "test-1");

        await importer.ImportAsync(FixtureEnPath);
        await importer.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var count = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wiktionary_entries WHERE lang_code = 'en'");

        // After re-import, we expect the same 6 entries (not duplicated).
        count.Should().Be(6);
    }

    [Fact]
    public async Task ImportAsync_WithDeferIndexes_LeavesIndexesMissingUntilRebuild()
    {
        // Bulk-build pattern (scripts/build-*-corpus-dump.sh): drop indexes
        // up-front via `reset-wiktionary`, import each language with
        // --defer-indexes (just COPY, no per-lang DROP/REBUILD), then
        // `rebuild-indexes` at the end restores them.
        await using (var setupConnection = await postgres.DataSource.OpenConnectionAsync())
        await using (var setupTx = await setupConnection.BeginTransactionAsync())
        {
            await WiktionaryIndexMaintenance.DropAsync(setupConnection, setupTx, default);
            await setupTx.CommitAsync();
        }

        var en = new WiktionaryImporter(postgres.DataSource, "en", "test-1", deferIndexes: true);
        var ru = new WiktionaryImporter(postgres.DataSource, "ru", "test-1", deferIndexes: true);

        await en.ImportAsync(FixtureEnPath);
        await ru.ImportAsync(FixtureRuPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var indexes = (await connection.QueryAsync<string>(
            """
            SELECT indexname FROM pg_indexes
            WHERE tablename = 'wiktionary_entries'
              AND indexname IN ('ix_wiktionary_entries_lookup', 'ix_wiktionary_entries_data')
            """)).ToArray();

        indexes.Should().BeEmpty();

        // Both languages' rows are in place despite no indexes.
        var totalRows = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wiktionary_entries");
        totalRows.Should().Be(6 + 3);  // EN fixture has 6, RU has 3

        // rebuild-indexes restores both indexes.
        await using var rebuildConnection = await postgres.DataSource.OpenConnectionAsync();
        await using var rebuildTx = await rebuildConnection.BeginTransactionAsync();
        await WiktionaryIndexMaintenance.DropAsync(rebuildConnection, rebuildTx, default);
        await WiktionaryIndexMaintenance.CreateAsync(rebuildConnection, rebuildTx, default);
        await rebuildTx.CommitAsync();

        var indexesAfter = (await connection.QueryAsync<string>(
            """
            SELECT indexname FROM pg_indexes
            WHERE tablename = 'wiktionary_entries'
              AND indexname IN ('ix_wiktionary_entries_lookup', 'ix_wiktionary_entries_data')
            """)).ToArray();

        indexesAfter.Should().BeEquivalentTo(new[]
        {
            "ix_wiktionary_entries_lookup",
            "ix_wiktionary_entries_data"
        });
    }

    [Fact]
    public async Task ImportAsync_WhenSourceLangCodeDisagreesWithFlag_FailsAndRollsBack()
    {
        // Seed some existing data under 'en' so we can confirm the
        // rollback restores it.
        var seed = new WiktionaryImporter(postgres.DataSource, "en", "seed");
        await seed.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        var countBefore = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wiktionary_entries WHERE lang_code = 'en'");
        countBefore.Should().Be(6);

        // Now try to import the RU fixture under --lang en. The
        // importer's pre-COPY DELETE would wipe the 6 en rows; without
        // the validation, the RU entries would then land with
        // lang_code='ru' while metadata gets recorded under
        // source_version_wiktionary_en. Guard must trip before any of
        // that happens.
        var mismatched = new WiktionaryImporter(postgres.DataSource, "en", "mismatched");

        var act = async () => await mismatched.ImportAsync(FixtureRuPath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lang_code='ru'*--lang was set to 'en'*");

        // Rollback restored the seeded state.
        var countAfter = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wiktionary_entries WHERE lang_code = 'en'");
        countAfter.Should().Be(6);

        // No RU rows were smuggled in.
        var ruCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wiktionary_entries WHERE lang_code = 'ru'");
        ruCount.Should().Be(0);

        // Metadata wasn't updated for the failed import.
        var metadataRows = (await connection.QueryAsync<string>(
            "SELECT key FROM corpus_metadata ORDER BY key")).ToArray();
        metadataRows.Should().ContainSingle()
            .Which.Should().Be("source_version_wiktionary_en");
    }

    [Fact]
    public async Task ImportAsync_OnMalformedJsonLine_FailsAndRollsBack()
    {
        // Seed some existing data so we can confirm rollback.
        var seed = new WiktionaryImporter(postgres.DataSource, "en", "seed");
        await seed.ImportAsync(FixtureEnPath);

        // Build a fixture with a valid entry, a malformed line, then
        // another valid entry. Writes to a temp file so we don't pollute
        // the committed fixtures directory.
        var tempPath = Path.Combine(Path.GetTempPath(), $"wiktionary-malformed-{Guid.NewGuid():N}.jsonl");
        try
        {
            await File.WriteAllLinesAsync(tempPath, new[]
            {
                """{"word":"book","lang_code":"en","pos":"noun","senses":[]}""",
                """{"word":"truncated","lang_code":"en",""",  // malformed — file truncated mid-object
                """{"word":"run","lang_code":"en","pos":"verb","senses":[]}"""
            });

            var importer = new WiktionaryImporter(postgres.DataSource, "en", "should-not-commit");

            var act = async () => await importer.ImportAsync(tempPath);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Malformed JSON on line 2*");

            // Rollback preserved the seeded data and did not advance
            // metadata past "seed".
            await using var connection = await postgres.DataSource.OpenConnectionAsync();
            var count = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM wiktionary_entries WHERE lang_code = 'en'");
            count.Should().Be(6);

            var sourceVersion = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT value FROM corpus_metadata WHERE key = 'source_version_wiktionary_en'");
            sourceVersion.Should().Be("seed");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ImportAsync_OnEntryMissingRequiredField_FailsAndRollsBack()
    {
        var seed = new WiktionaryImporter(postgres.DataSource, "en", "seed");
        await seed.ImportAsync(FixtureEnPath);

        var tempPath = Path.Combine(Path.GetTempPath(), $"wiktionary-missing-field-{Guid.NewGuid():N}.jsonl");
        try
        {
            await File.WriteAllLinesAsync(tempPath, new[]
            {
                """{"word":"book","lang_code":"en","pos":"noun","senses":[]}""",
                """{"word":"nopos","lang_code":"en","pos":"","senses":[]}"""  // empty pos
            });

            var importer = new WiktionaryImporter(postgres.DataSource, "en", "should-not-commit");

            var act = async () => await importer.ImportAsync(tempPath);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*missing a required field*");

            await using var connection = await postgres.DataSource.OpenConnectionAsync();
            var count = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM wiktionary_entries WHERE lang_code = 'en'");
            count.Should().Be(6);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ImportAsync_RecordsSourceVersionInMetadata()
    {
        var importer = new WiktionaryImporter(postgres.DataSource, "en", "v2026.04.15");

        await importer.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var version = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT value FROM corpus_metadata WHERE key = 'source_version_wiktionary_en'");

        version.Should().Be("v2026.04.15");
    }
}
