using Dapper;
using FluentAssertions;
using Langoose.Corpus.DbTool;
using Langoose.Corpus.DbTool.Importers;
using Langoose.Corpus.IntegrationTests.Infrastructure;
using Xunit;

namespace Langoose.Corpus.IntegrationTests;

public sealed class WordfreqImporterTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string FixtureEnPath = "fixtures/wordfreq-en-sample.tsv";

    public async Task InitializeAsync()
    {
        var initializer = new CorpusInitializer(postgres.DataSource);

        await initializer.ApplySchemaAsync();

        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE TABLE wordfreq_rankings;
            DELETE FROM corpus_metadata;
            """;
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ImportAsync_FromEnglishFixture_LoadsAllRankings()
    {
        var importer = new WordfreqImporter(postgres.DataSource, "en", "wordfreq-test-1");

        var summary = await importer.ImportAsync(FixtureEnPath);

        summary.Source.Should().Be("wordfreq_en");
        summary.SourceVersion.Should().Be("wordfreq-test-1");
        summary.EntriesImported.Should().Be(10);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var rows = (await connection.QueryAsync<(string word, int rank, decimal zipfScore, string source)>(
            "SELECT word, rank, zipf_score, source FROM wordfreq_rankings WHERE lang_code = 'en' ORDER BY rank")).ToArray();

        rows.Should().HaveCount(10);
        rows[0].Should().Be(("the", 1, 7.85m, "wordfreq-test-1"));
        rows[4].Should().Be(("book", 5, 5.20m, "wordfreq-test-1"));
        rows[9].Should().Be(("unicorn", 10, 3.20m, "wordfreq-test-1"));
    }

    [Fact]
    public async Task ImportAsync_RunTwiceWithSameSource_ReplacesPreviousRows()
    {
        var importer = new WordfreqImporter(postgres.DataSource, "en", "wordfreq-test-1");

        await importer.ImportAsync(FixtureEnPath);
        await importer.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var count = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wordfreq_rankings WHERE lang_code = 'en'");

        count.Should().Be(10);
    }

    [Fact]
    public async Task ImportAsync_DifferentSources_CoexistForSameLanguage()
    {
        // The PK (lang_code, word, source) is what enables this — a future
        // SUBTLEX import for English can land alongside wordfreq without
        // either wiping the other.
        var primary = new WordfreqImporter(postgres.DataSource, "en", "wordfreq-2026-04-01");
        var alternative = new WordfreqImporter(postgres.DataSource, "en", "wordfreq-2026-04-25");

        await primary.ImportAsync(FixtureEnPath);
        await alternative.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var totalRows = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wordfreq_rankings WHERE lang_code = 'en'");
        totalRows.Should().Be(20);

        var sources = (await connection.QueryAsync<string>(
            "SELECT DISTINCT source FROM wordfreq_rankings WHERE lang_code = 'en' ORDER BY source")).ToArray();
        sources.Should().Equal("wordfreq-2026-04-01", "wordfreq-2026-04-25");
    }

    [Fact]
    public async Task ImportAsync_RecordsSourceVersionInMetadata()
    {
        var importer = new WordfreqImporter(postgres.DataSource, "en", "wordfreq-large-3.1.1");

        await importer.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();

        var version = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT value FROM corpus_metadata WHERE key = 'source_version_wordfreq_en'");

        version.Should().Be("wordfreq-large-3.1.1");
    }

    [Fact]
    public async Task ImportAsync_OnMalformedTsvLine_FailsAndRollsBack()
    {
        var seed = new WordfreqImporter(postgres.DataSource, "en", "seed");
        await seed.ImportAsync(FixtureEnPath);

        var tempPath = Path.Combine(Path.GetTempPath(), $"wordfreq-malformed-{Guid.NewGuid():N}.tsv");
        try
        {
            // Mix valid + bad lines. The bad line has only 2 columns —
            // the parser must trip before COPY COMMITs and rollback must
            // preserve the seeded data.
            await File.WriteAllLinesAsync(tempPath, new[]
            {
                "the\t1\t7.85",
                "broken\t2",
                "be\t3\t7.55"
            });

            var importer = new WordfreqImporter(postgres.DataSource, "en", "should-not-commit");

            var act = async () => await importer.ImportAsync(tempPath);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Malformed TSV on line 2*");

            await using var connection = await postgres.DataSource.OpenConnectionAsync();
            var seedRowCount = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM wordfreq_rankings WHERE lang_code = 'en' AND source = 'seed'");
            seedRowCount.Should().Be(10);

            var attemptedRowCount = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM wordfreq_rankings WHERE source = 'should-not-commit'");
            attemptedRowCount.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ResetWordfreq_TruncatesTableAndClearsMetadata()
    {
        // Seed two distinct (lang, source) row sets. The naive
        // import-wordfreq DELETE only catches one pair at a time; the
        // reset has to wipe everything.
        var enImporter = new WordfreqImporter(postgres.DataSource, "en", "wordfreq-2026-04-01");
        var ruImporter = new WordfreqImporter(postgres.DataSource, "ru", "wordfreq-2026-04-25");
        await enImporter.ImportAsync(FixtureEnPath);
        // RU fixture isn't committed (see WiktionaryImporterTests), so
        // re-use the EN fixture under the ru lang_code — the importer
        // doesn't validate the words against the lang code, and the
        // assertion only cares that BOTH (lang, source) pairs get wiped.
        await ruImporter.ImportAsync(FixtureEnPath);

        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        var beforeRows = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wordfreq_rankings");
        var beforeMetadataKeys = (await connection.QueryAsync<string>(
            "SELECT key FROM corpus_metadata WHERE key LIKE 'source_version_wordfreq_%' ORDER BY key")).ToArray();

        beforeRows.Should().Be(20);
        beforeMetadataKeys.Should().Equal("source_version_wordfreq_en", "source_version_wordfreq_ru");

        // Invoke the same SQL the reset-wordfreq subcommand runs.
        await using (var resetCmd = connection.CreateCommand())
        {
            resetCmd.CommandText = """
                TRUNCATE TABLE wordfreq_rankings;
                DELETE FROM corpus_metadata
                    WHERE key LIKE 'source_version_wordfreq_%';
                """;
            await resetCmd.ExecuteNonQueryAsync();
        }

        var afterRows = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM wordfreq_rankings");
        var afterMetadataKeys = (await connection.QueryAsync<string>(
            "SELECT key FROM corpus_metadata WHERE key LIKE 'source_version_wordfreq_%'")).ToArray();

        afterRows.Should().Be(0);
        afterMetadataKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportAsync_OnInvalidRank_FailsAndRollsBack()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"wordfreq-bad-rank-{Guid.NewGuid():N}.tsv");
        try
        {
            await File.WriteAllLinesAsync(tempPath, new[]
            {
                "the\t1\t7.85",
                "be\tnotanint\t7.55"
            });

            var importer = new WordfreqImporter(postgres.DataSource, "en", "test");

            var act = async () => await importer.ImportAsync(tempPath);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Invalid rank*line 2*");
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
