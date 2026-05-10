using Dapper;
using FluentAssertions;
using Langoose.Corpus.DbTool;
using Langoose.Corpus.DbTool.Importers;
using Langoose.Corpus.IntegrationTests.Infrastructure;
using Xunit;

namespace Langoose.Corpus.IntegrationTests;

public sealed class TatoebaImporterTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string FixtureDir = "fixtures/tatoeba";

    public async Task InitializeAsync()
    {
        var initializer = new CorpusInitializer(postgres.DataSource);

        await initializer.ApplySchemaAsync();

        // Reset to a clean slate before each test. The container itself
        // is reused across the class via PostgresFixture; per-test reset
        // makes ordering irrelevant.
        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var partitions = await TatoebaIndexMaintenance.ListPartitionLangCodesAsync(
            connection, transaction, default);
        foreach (var lang in partitions)
        {
            await TatoebaIndexMaintenance.DropPartitionAsync(
                connection, transaction, lang, default);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                TRUNCATE TABLE tatoeba_links;
                DELETE FROM corpus_metadata WHERE key LIKE 'source_tatoeba_%';
                """;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ImportAsync_FromFixture_LoadsSentencesIntoBothPartitions()
    {
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "test-1");

        var summary = await importer.ImportAsync(FixtureDir);

        summary.Source.Should().Be("tatoeba_en_ru");
        summary.SourceVersion.Should().Be("test-1");
        // 5 EN + 5 RU sentences + 6 cross-language links (out of 8
        // candidate links — 2 have at least one endpoint not imported).
        summary.EntriesImported.Should().Be(5 + 5 + 6);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var enRows = (await connection.QueryAsync<(long sentenceId, string text)>(
            """
            SELECT sentence_id, text FROM tatoeba_sentences
            WHERE lang_code = 'en' ORDER BY sentence_id
            """)).ToArray();
        enRows.Should().BeEquivalentTo(new[]
        {
            (1001L, "I read a book."),
            (1002L, "The cat is on the table."),
            (1003L, "She runs every morning."),
            (1004L, "Hello!"),
            (1005L, "Good morning.")
        });

        var ruRows = (await connection.QueryAsync<(long sentenceId, string text)>(
            """
            SELECT sentence_id, text FROM tatoeba_sentences
            WHERE lang_code = 'ru' ORDER BY sentence_id
            """)).ToArray();
        ruRows.Should().HaveCount(5);
        ruRows[0].sentenceId.Should().Be(2001);
        ruRows[0].text.Should().Be("Я читаю книгу.");
    }

    [Fact]
    public async Task ImportAsync_FollowsLinkFromEnglishToPairedRussianSentence()
    {
        // The AC integration test: import a tiny fixture, query a known
        // sentence by (lang_code, sentence_id), follow a link to the paired
        // sentence. This is the round-trip the downstream context generator
        // needs to do for every materialised EntryContext.
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "test-1");

        await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        // Look up the English sentence "I read a book." by (lang, id).
        var englishText = await connection.QuerySingleAsync<string>(
            """
            SELECT text FROM tatoeba_sentences
            WHERE lang_code = 'en' AND sentence_id = 1001
            """);
        englishText.Should().Be("I read a book.");

        // Follow the link to the Russian translation. tatoeba_links stores
        // both directions, so a single forward lookup suffices.
        var russianText = await connection.QuerySingleAsync<string>(
            """
            SELECT s.text
            FROM tatoeba_links l
            JOIN tatoeba_sentences s
              ON s.lang_code = 'ru' AND s.sentence_id = l.target_id
            WHERE l.source_id = 1001
            """);
        russianText.Should().Be("Я читаю книгу.");
    }

    [Fact]
    public async Task ImportAsync_DropsLinksWhereEitherEndpointIsNotImported()
    {
        // Fixture's links.tsv contains:
        //   1001↔2001, 1002↔2002, 1003↔2003 (6 rows, all imported)
        //   1099→2099   (neither end imported)
        //   1001→9999   (only one end imported)
        // Filtering must drop the last two — keeping them would create
        // dangling FKs once a future schema enforces them, and pollutes
        // the EntryContext materialisation step (#115).
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "test-1");

        await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var totalLinks = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tatoeba_links");
        totalLinks.Should().Be(6);

        var orphanLinks = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*) FROM tatoeba_links
            WHERE source_id NOT IN (SELECT sentence_id FROM tatoeba_sentences)
               OR target_id NOT IN (SELECT sentence_id FROM tatoeba_sentences)
            """);
        orphanLinks.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WithMaxPairs_KeepsBothDirectionsForBudgetPairs()
    {
        // Fixture has 6 cross-language link rows = 3 canonical pairs:
        // (1001,2001) bidirectional, (1002,2002) bidirectional,
        // (1003,2003) bidirectional. With max-pairs=2 we keep the first
        // two canonical pairs in (lo, hi) order: (1001,2001) and
        // (1002,2002). Each contributes BOTH directions, so the dump
        // ends up with 4 link rows. Sentences for the third pair
        // (1003, 2003) are dropped as orphans.
        var importer = new TatoebaImporter(
            postgres.DataSource, "en", "ru", "test-1", maxPairs: 2);

        var summary = await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var linkPairs = (await connection.QueryAsync<(long src, long tgt)>(
            "SELECT source_id, target_id FROM tatoeba_links ORDER BY source_id, target_id")).ToArray();
        // Both directions for each kept canonical pair:
        linkPairs.Should().Equal(
            (1001L, 2001L), (1002L, 2002L),
            (2001L, 1001L), (2002L, 1002L));

        var enIds = (await connection.QueryAsync<long>(
            "SELECT sentence_id FROM tatoeba_sentences WHERE lang_code = 'en' ORDER BY sentence_id")).ToArray();
        enIds.Should().Equal(1001L, 1002L);

        var ruIds = (await connection.QueryAsync<long>(
            "SELECT sentence_id FROM tatoeba_sentences WHERE lang_code = 'ru' ORDER BY sentence_id")).ToArray();
        ruIds.Should().Equal(2001L, 2002L);

        // 2 EN + 2 RU sentences + 4 link rows.
        summary.EntriesImported.Should().Be(2 + 2 + 4);
    }

    [Fact]
    public async Task ImportAsync_WithMaxPairs_LeavesNoOrphanSentencesAndKeepsBothDirections()
    {
        // Two invariants for any --max-pairs run:
        //   1. Every sentence participates in at least one link.
        //   2. Every kept (a→b) row has its (b→a) sibling — downstream
        //      forward lookups by source_id find the translation
        //      regardless of which language was the "source" side.
        var importer = new TatoebaImporter(
            postgres.DataSource, "en", "ru", "test-1", maxPairs: 1);

        await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var orphans = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*) FROM tatoeba_sentences s
            WHERE NOT EXISTS (SELECT 1 FROM tatoeba_links l WHERE l.source_id = s.sentence_id)
              AND NOT EXISTS (SELECT 1 FROM tatoeba_links l WHERE l.target_id = s.sentence_id)
            """);
        orphans.Should().Be(0);

        var unidirectional = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*) FROM tatoeba_links a
            WHERE NOT EXISTS (
                SELECT 1 FROM tatoeba_links b
                WHERE b.source_id = a.target_id AND b.target_id = a.source_id
            )
            """);
        unidirectional.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_RunTwice_ReplacesPreviousRows()
    {
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "test-1");

        await importer.ImportAsync(FixtureDir);
        await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var sentenceCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tatoeba_sentences");
        sentenceCount.Should().Be(10);

        var linkCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tatoeba_links");
        linkCount.Should().Be(6);
    }

    [Fact]
    public async Task ImportAsync_FirstRunForLanguagePair_CreatesBothPartitions()
    {
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "test-1");

        await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var partitions = (await connection.QueryAsync<string>(
            """
            SELECT c.relname
            FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid
            JOIN pg_class p ON p.oid = i.inhparent
            WHERE p.relname = 'tatoeba_sentences'
            ORDER BY c.relname
            """)).ToArray();
        partitions.Should().Equal("tatoeba_sentences_en", "tatoeba_sentences_ru");
    }

    [Fact]
    public async Task ImportAsync_RecordsSourceVersionInMetadata()
    {
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "tatoeba-2026-05-03");

        await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var version = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT value FROM corpus_metadata WHERE key = 'source_tatoeba_en_ru'");

        version.Should().Be("tatoeba-2026-05-03");
    }

    [Fact]
    public async Task ImportAsync_OnMalformedSentenceLine_FailsAndRollsBack()
    {
        // Seed fixture so we can confirm rollback restores it.
        var seed = new TatoebaImporter(postgres.DataSource, "en", "ru", "seed");
        await seed.ImportAsync(FixtureDir);

        var tempDir = Path.Combine(Path.GetTempPath(), $"tatoeba-malformed-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // EN file is malformed (only 2 cols), RU + links are well-formed.
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "en_sentences.tsv"), new[]
            {
                "1001\teng\tValid line.",
                "1002\tjust two cols",
            });
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "ru_sentences.tsv"), new[]
            {
                "2001\trus\tВалидная строка."
            });
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "links.tsv"), new[]
            {
                "1001\t2001"
            });

            var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "should-not-commit");

            var act = async () => await importer.ImportAsync(tempDir);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Malformed TSV on line 2*");

            await using var connection = await postgres.DataSource.OpenConnectionAsync();
            var rowCount = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM tatoeba_sentences");
            rowCount.Should().Be(10);  // seed survived

            var sourceVersion = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT value FROM corpus_metadata WHERE key = 'source_tatoeba_en_ru'");
            sourceVersion.Should().Be("seed");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_OnMalformedLinkLine_FailsAndRollsBack()
    {
        var seed = new TatoebaImporter(postgres.DataSource, "en", "ru", "seed");
        await seed.ImportAsync(FixtureDir);

        var tempDir = Path.Combine(Path.GetTempPath(), $"tatoeba-bad-link-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "en_sentences.tsv"), new[]
            {
                "1001\teng\tValid."
            });
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "ru_sentences.tsv"), new[]
            {
                "2001\trus\tВалидная."
            });
            // Second link has a non-numeric source_id.
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "links.tsv"), new[]
            {
                "1001\t2001",
                "notanint\t9999"
            });

            var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "should-not-commit");

            var act = async () => await importer.ImportAsync(tempDir);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Invalid source_id on line 2*");

            await using var connection = await postgres.DataSource.OpenConnectionAsync();
            var sentenceCount = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM tatoeba_sentences");
            sentenceCount.Should().Be(10);  // seed survived
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_WithUnsafeLangCode_RejectsBeforeIssuingDdl()
    {
        // Defence in depth: lang_code is interpolated into partition DDL,
        // so anything that isn't a strict identifier must throw before any
        // DDL is issued. Mirrors the WiktionaryImporter contract test.
        var importer = new TatoebaImporter(
            postgres.DataSource, "en'; DROP TABLE--", "ru", "test-1");

        var act = async () => await importer.ImportAsync(FixtureDir);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid lang_code*");
    }

    [Fact]
    public async Task ImportAsync_WhenLangAndPairLangAreEqual_Throws()
    {
        var importer = new TatoebaImporter(postgres.DataSource, "en", "en", "test-1");

        var act = async () => await importer.ImportAsync(FixtureDir);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*--lang and --pair-lang must differ*");
    }

    [Fact]
    public async Task ImportAsync_WhenSourceDirectoryMissing_Throws()
    {
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "test-1");

        var act = async () => await importer.ImportAsync("does-not-exist-dir");

        await act.Should().ThrowAsync<DirectoryNotFoundException>()
            .WithMessage("*Tatoeba source directory not found*");
    }

    [Fact]
    public async Task ResetTatoeba_DropsPartitionsAndClearsLinksAndMetadata()
    {
        var importer = new TatoebaImporter(postgres.DataSource, "en", "ru", "test-1");
        await importer.ImportAsync(FixtureDir);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var beforeSentences = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tatoeba_sentences");
        var beforeLinks = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tatoeba_links");
        var beforeMetadata = (await connection.QueryAsync<string>(
            "SELECT key FROM corpus_metadata WHERE key LIKE 'source_tatoeba_%'")).ToArray();

        beforeSentences.Should().Be(10);
        beforeLinks.Should().Be(6);
        beforeMetadata.Should().Equal("source_tatoeba_en_ru");

        // Invoke the same SQL the reset-tatoeba subcommand runs.
        await using (var resetTx = await connection.BeginTransactionAsync())
        {
            var partitions = await TatoebaIndexMaintenance.ListPartitionLangCodesAsync(
                connection, resetTx, default);
            foreach (var lang in partitions)
            {
                await TatoebaIndexMaintenance.DropPartitionAsync(
                    connection, resetTx, lang, default);
            }
            await using (var resetCmd = connection.CreateCommand())
            {
                resetCmd.Transaction = resetTx;
                resetCmd.CommandText = """
                    TRUNCATE TABLE tatoeba_links;
                    DELETE FROM corpus_metadata WHERE key LIKE 'source_tatoeba_%';
                    """;
                await resetCmd.ExecuteNonQueryAsync();
            }
            await resetTx.CommitAsync();
        }

        var afterSentences = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tatoeba_sentences");
        var afterLinks = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM tatoeba_links");
        var afterMetadata = (await connection.QueryAsync<string>(
            "SELECT key FROM corpus_metadata WHERE key LIKE 'source_tatoeba_%'")).ToArray();
        var afterPartitions = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*) FROM pg_inherits i
            JOIN pg_class p ON p.oid = i.inhparent
            WHERE p.relname = 'tatoeba_sentences'
            """);

        afterSentences.Should().Be(0);
        afterLinks.Should().Be(0);
        afterMetadata.Should().BeEmpty();
        afterPartitions.Should().Be(0);
    }
}
