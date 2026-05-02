using System.Text.Json;
using FluentAssertions;
using Langoose.Core.Heuristic;
using Langoose.Core.Configuration;
using Langoose.Core.Services;
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
using Langoose.Domain.Services;
using Langoose.Worker.Configuration;
using Langoose.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Langoose.Corpus.IntegrationTests;

/// <summary>
/// End-to-end integration test for <see cref="CorpusImportJob"/>: stands
/// up corpus + app schemas in the same Postgres container, seeds the
/// corpus from fixture files, submits a Pending background job, and
/// runs the job's per-claim dispatch directly via
/// <see cref="CorpusImportJob.RunOnceAsync"/>. Asserts that import-entry
/// rows land with the expected statuses and that the job advances to
/// Completed (or Failed) with a matching ExecutionState summary —
/// exercising the full claim → service-stream → persist → mark cycle.
/// </summary>
public sealed class CorpusImportJobTests(PostgresFixture postgres)
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
    public async Task RunOnceAsync_ProcessesAllRankedBundles_InOneRunWhenSourceFitsBatch()
    {
        await SubmitJobAsync();
        var job = BuildJob(batchSize: 100);

        // 5 source rows < batchSize=100, so the service knows the source
        // is exhausted on the first fetch and signals end-of-chain by
        // returning Cursor=null. No continuation is queued.
        await DrainAsync(job);

        await using var db = new AppDbContext(_appDbOptions);

        // Fixture has 5 (ranked) en wiktionary keys: book, run, good, London, lead.
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

        staged.Single(x => x.Text == "London").Status.Should().Be(ImportEntryStatus.HeuristicRejected);
        staged.Single(x => x.Text == "London").StatusReason.Should().Contain("blocklist");
        staged.Where(x => x.Text != "London").Should().OnlyContain(x => x.Status == ImportEntryStatus.HeuristicAccepted);

        var lead = staged.Single(x => x.Text == "lead");
        using var leadDoc = JsonDocument.Parse(lead.Payload);
        leadDoc.RootElement.GetProperty("senses").GetArrayLength().Should().Be(2);

        // Single Completed row — no terminator scheduled.
        var rows = await db.BackgroundJobs
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].Status.Should().Be(JobStatus.Completed);

        var state = JsonSerializer.Deserialize<BulkJobState>(rows[0].ExecutionState!, AppJsonOptions.Default)!;
        state.TotalCount.Should().Be(5, "batch returned 5 source rows");
        state.ProcessedCount.Should().Be(5, "all 5 inserted into import_entries (status differs)");
        state.FailedCount.Should().Be(0, "no dedup'd items on first import");
        state.Cursor.Should().BeNull("partial batch (5 < 100) — source exhausted, no continuation");
    }

    [Fact]
    public async Task RunOnceAsync_IsIdempotent_OnRerun()
    {
        await SubmitJobAsync();
        var job = BuildJob(batchSize: 100);
        await DrainAsync(job);

        // Re-submit a second initial job with the same settings; existing
        // rows should be skipped (unique source_ref_id), not duplicated.
        await SubmitJobAsync();
        await DrainAsync(job);

        await using var db = new AppDbContext(_appDbOptions);
        var staged = await db.ImportEntries.CountAsync();
        staged.Should().Be(5);
    }

    [Fact]
    public async Task RunOnceAsync_WhenNoPendingJobs_ReturnsFalse()
    {
        var job = BuildJob(batchSize: 100);
        var dispatched = await job.RunOnceAsync(CancellationToken.None);
        dispatched.Should().BeFalse();
    }

    [Fact]
    public async Task RunOnceAsync_FailsCleanly_WhenCorpusSnapshotMissing()
    {
        var settings = new CorpusImportParams("en", "wiktionary-missing-version", StartCursor: null);
        await SubmitJobAsync(settings);

        var job = BuildJob(batchSize: 100);
        await job.RunOnceAsync(CancellationToken.None);

        await using var db = new AppDbContext(_appDbOptions);
        var jobRow = await db.BackgroundJobs.AsNoTracking().SingleAsync();

        jobRow.Status.Should().Be(JobStatus.Failed);

        var state = JsonSerializer.Deserialize<BulkJobState>(jobRow.ExecutionState!, AppJsonOptions.Default)!;
        state.ErrorMessage.Should().Contain("wiktionary-missing-version");
    }

    [Fact]
    public async Task RunOnceAsync_FailsCleanly_WhenLanguageNotImportedForSource()
    {
        var settings = new CorpusImportParams("fr", Source, StartCursor: null);
        await SubmitJobAsync(settings);

        var job = BuildJob(batchSize: 100);
        await job.RunOnceAsync(CancellationToken.None);

        await using var db = new AppDbContext(_appDbOptions);
        var jobRow = await db.BackgroundJobs.AsNoTracking().SingleAsync();

        jobRow.Status.Should().Be(JobStatus.Failed);

        var state = JsonSerializer.Deserialize<BulkJobState>(jobRow.ExecutionState!, AppJsonOptions.Default)!;
        state.ErrorMessage.Should().Contain("'fr'");
    }

    [Fact]
    public async Task RunOnceAsync_CreatesOneRowPerBatch_AcrossChain()
    {
        await SubmitJobAsync();
        var job = BuildJob(batchSize: 2);

        await DrainAsync(job);

        await using var db = new AppDbContext(_appDbOptions);
        var staged = await db.ImportEntries.CountAsync();
        staged.Should().Be(5);

        // 5 items at batch=2 → batches of 2, 2, 1. The third batch came
        // back partial (1 < 2) so the service sets Cursor=null on it,
        // ending the chain immediately. No terminator round-trip.
        var rows = await db.BackgroundJobs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(x => x.Status == JobStatus.Completed);

        var states = rows
            .Select(r => JsonSerializer.Deserialize<BulkJobState>(r.ExecutionState!, AppJsonOptions.Default)!)
            .ToList();
        states[..^1].Should().OnlyContain(s => s.Cursor != null);
        states[^1].Cursor.Should().BeNull("last batch was partial — chain terminated without a separate empty run");
    }

    private static async Task DrainAsync(CorpusImportJob job)
    {
        // Each RunOnceAsync claims+processes one Pending row. Auto-created
        // continuations land back in the queue; loop until no Pending
        // remains (claim returns false). Bounded to keep buggy chains
        // from spinning forever.
        for (var i = 0; i < 100; i++)
        {
            if (!await job.RunOnceAsync(CancellationToken.None))
                return;
        }

        throw new InvalidOperationException("Drain exceeded 100 iterations — chain did not terminate.");
    }

    private async Task<Guid> SubmitJobAsync(CorpusImportParams? settings = null)
    {
        settings ??= new CorpusImportParams("en", Source, StartCursor: null);

        await using var db = new AppDbContext(_appDbOptions);
        var now = DateTimeOffset.UtcNow;
        var job = new BackgroundJob
        {
            Id = Guid.CreateVersion7(),
            Type = JobType.CorpusImport,
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

    private CorpusImportJob BuildJob(int batchSize)
    {
        var heuristicSettings = new HeuristicFilterSettings
        {
            MinLength = 2,
            MaxLength = 300,
            PosBlocklist = ["name", "abbrev", "symbol", "intj"]
        };
        var dbFactory = new TestDbContextFactory(_appDbOptions);
        var reader = new WiktionaryImportSourceReader(postgres.DataSource);
        var heuristic = new HeuristicFilter(Options.Create(heuristicSettings));

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(dbFactory);
        services.AddSingleton<IImportSourceReader>(reader);
        services.AddSingleton(heuristic);
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
            typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        services.AddScoped<ICorpusImportService, CorpusImportService>();

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new CorpusImportSettings
        {
            PollIntervalSeconds = 5,
            BatchSize = batchSize
        });

        return new CorpusImportJob(scopeFactory, dbFactory, options, NullLogger<CorpusImportJob>.Instance);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
