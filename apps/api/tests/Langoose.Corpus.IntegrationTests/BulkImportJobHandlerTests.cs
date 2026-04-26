using System.Text.Json;
using FluentAssertions;
using Langoose.Core.BulkImport;
using Langoose.Core.Configuration;
using Langoose.Worker.Configuration;
using Langoose.Corpus.Data.Readers;
using Langoose.Corpus.DbTool;
using Langoose.Corpus.DbTool.Importers;
using Langoose.Corpus.IntegrationTests.Infrastructure;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Imports;
using Langoose.Domain.Jobs;
using Langoose.Domain.Models;
using Langoose.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Langoose.Corpus.IntegrationTests;

/// <summary>
/// End-to-end integration test for <see cref="BulkImportJobHandler"/>:
/// stands up corpus + app schemas in the same Postgres container, seeds
/// the corpus from fixture files, submits a Pending background job, and
/// runs the handler directly. Asserts that import-entry rows land with the
/// expected statuses and that the job advances to Completed with a
/// matching ExecutionState summary.
/// </summary>
public sealed class BulkImportJobHandlerTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string FixtureEnPath = "fixtures/wiktionary-en-sample.jsonl";
    private const string FixtureWordfreqEnPath = "fixtures/wordfreq-en-sample.tsv";
    private const string Source = "wiktionary-test-1";
    private const string WordfreqSource = "wordfreq-test-1";

    private DbContextOptions<AppDbContext> _appDbOptions = null!;

    public async Task InitializeAsync()
    {
        var corpusInit = new CorpusInitializer(postgres.DataSource);
        await corpusInit.ApplySchemaAsync();

        // Wipe corpus state so each test runs against a clean slate.
        await using (var connection = await postgres.DataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var partitions = await WiktionaryIndexMaintenance.ListPartitionLangCodesAsync(
                connection, transaction, default);

            foreach (var lang in partitions)
            {
                await WiktionaryIndexMaintenance.DropPartitionAsync(
                    connection, transaction, lang, default);
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    TRUNCATE TABLE wordfreq_rankings;
                    DELETE FROM corpus_metadata;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        // Apply app schema in the same database. Corpus tables and app
        // tables don't share names, so they coexist cleanly. Migration
        // is idempotent across tests; per-test data reset via TRUNCATE.
        _appDbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using (var db = new AppDbContext(_appDbOptions))
        {
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE import_entries, background_jobs RESTART IDENTITY CASCADE;");
        }

        // Seed corpus.
        var wikt = new WiktionaryImporter(postgres.DataSource, "en", Source);
        await wikt.ImportAsync(FixtureEnPath);

        var wf = new WordfreqImporter(postgres.DataSource, "en", WordfreqSource);
        await wf.ImportAsync(FixtureWordfreqEnPath);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RunAsync_ProcessesAllRankedBundles_AndAdvancesJobToCompleted()
    {
        var jobId = await SubmitJobAsync();
        var handler = BuildHandler(batchSize: 100);

        await handler.RunAsync(jobId, default);

        await using var db = new AppDbContext(_appDbOptions);

        // Fixture has 5 (ranked) en wiktionary keys: book, run, good, London, lead.
        // "lead" has two etymology rows that bundle into one import-entry row.
        // Total bundles: 5 (book/noun, good/adj, London/name, lead/noun, run/verb).
        var staged = await db.ImportEntries
            .Where(x => x.Source == EntrySource.Wiktionary)
            .OrderBy(x => x.Text).ThenBy(x => x.PartOfSpeech)
            .ToListAsync();

        staged.Should().HaveCount(5);
        staged.Select(x => (x.Text, x.PartOfSpeech)).Should().BeEquivalentTo(new[]
        {
            ("London", "name"),
            ("book", "noun"),
            ("good", "adj"),
            ("lead", "noun"),
            ("run", "verb")
        });

        // London/name is rejected by the POS blocklist; rest accepted.
        staged.Single(x => x.Text == "London").Status.Should().Be(ImportEntryStatus.HeuristicRejected);
        staged.Single(x => x.Text == "London").StatusReason.Should().Contain("blocklist");
        staged.Where(x => x.Text != "London").Should().OnlyContain(x => x.Status == ImportEntryStatus.HeuristicAccepted);

        // The "lead" import-entry row bundles both etymology rows under
        // one payload — the typed payload concatenates senses across the
        // two source rows into a single Senses[] array.
        var lead = staged.Single(x => x.Text == "lead");
        using var leadDoc = JsonDocument.Parse(lead.Payload);
        leadDoc.RootElement.GetProperty("senses").GetArrayLength().Should().Be(2);

        var job = await db.BackgroundJobs.AsNoTracking().FirstAsync(x => x.Id == jobId);
        job.Status.Should().Be(JobStatus.Completed);

        var state = JsonSerializer.Deserialize<BulkImportState>(job.ExecutionState!, AppJsonOptions.Default)!;
        state.ProcessedCount.Should().Be(5);
        state.HeuristicAcceptedCount.Should().Be(4);
        state.HeuristicRejectedCount.Should().Be(1);
        state.ErrorMessage.Should().BeNull();
        state.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_OnRerun()
    {
        var jobId = await SubmitJobAsync();
        var handler = BuildHandler(batchSize: 100);
        await handler.RunAsync(jobId, default);

        // Re-submit a second job with the same settings; existing rows
        // should be skipped (unique source_ref_id), not duplicated.
        var jobId2 = await SubmitJobAsync();
        await handler.RunAsync(jobId2, default);

        await using var db = new AppDbContext(_appDbOptions);
        var staged = await db.ImportEntries.CountAsync();
        staged.Should().Be(5);
    }

    [Fact]
    public async Task RunAsync_FailsCleanly_WhenCorpusSnapshotMissing()
    {
        var settings = new BulkImportParams("en", "wiktionary-missing-version");
        var jobId = await SubmitJobAsync(settings);

        var handler = BuildHandler(batchSize: 100);
        await handler.RunAsync(jobId, default);

        await using var db = new AppDbContext(_appDbOptions);
        var job = await db.BackgroundJobs.AsNoTracking().FirstAsync(x => x.Id == jobId);

        job.Status.Should().Be(JobStatus.Failed);

        var state = JsonSerializer.Deserialize<BulkImportState>(job.ExecutionState!, AppJsonOptions.Default)!;
        state.ErrorMessage.Should().Contain("wiktionary-missing-version");
    }

    [Fact]
    public async Task RunAsync_FailsCleanly_WhenLanguageNotImportedForSource()
    {
        // Source exists with rows for 'en', but the operator asked for 'fr'.
        // Without the language filter the existence check would pass and the
        // job would silently complete with zero rows.
        var settings = new BulkImportParams("fr", Source);
        var jobId = await SubmitJobAsync(settings);

        var handler = BuildHandler(batchSize: 100);
        await handler.RunAsync(jobId, default);

        await using var db = new AppDbContext(_appDbOptions);
        var job = await db.BackgroundJobs.AsNoTracking().FirstAsync(x => x.Id == jobId);

        job.Status.Should().Be(JobStatus.Failed);

        var state = JsonSerializer.Deserialize<BulkImportState>(job.ExecutionState!, AppJsonOptions.Default)!;
        state.ErrorMessage.Should().Contain("'fr'");
    }

    [Fact]
    public async Task RunAsync_CommitsBatchByBatch_WithCorrectCursor()
    {
        var jobId = await SubmitJobAsync();
        var handler = BuildHandler(batchSize: 2);

        await handler.RunAsync(jobId, default);

        await using var db = new AppDbContext(_appDbOptions);
        var staged = await db.ImportEntries.CountAsync();
        staged.Should().Be(5);

        var job = await db.BackgroundJobs.AsNoTracking().FirstAsync(x => x.Id == jobId);
        job.Status.Should().Be(JobStatus.Completed);
    }

    private async Task<Guid> SubmitJobAsync(BulkImportParams? settings = null)
    {
        settings ??= new BulkImportParams("en", Source);

        await using var db = new AppDbContext(_appDbOptions);
        var now = DateTimeOffset.UtcNow;
        var job = new BackgroundJob
        {
            Id = Guid.CreateVersion7(),
            Type = JobType.BulkImport,
            Status = JobStatus.Pending,
            Settings = JsonSerializer.Serialize(settings, AppJsonOptions.Default),
            ExecutionState = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.BackgroundJobs.Add(job);
        await db.SaveChangesAsync();

        return job.Id;
    }

    private BulkImportJobHandler BuildHandler(int batchSize)
    {
        var dbFactory = new TestDbContextFactory(_appDbOptions);
        var reader = new WiktionaryImportSourceReader(postgres.DataSource);
        var heuristic = new HeuristicFilter(new HeuristicFilterSettings());
        var options = Options.Create(new BulkImportSettings
        {
            BatchSize = batchSize,
            Heuristic = new HeuristicFilterSettings()
        });

        return new BulkImportJobHandler(
            dbFactory, reader, heuristic, options,
            NullLogger<BulkImportJobHandler>.Instance);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
