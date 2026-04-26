using FluentAssertions;
using Langoose.Core.Providers;
using Langoose.Core.Services;
using Langoose.Data;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Langoose.Core.UnitTests.Services;

public sealed class EnrichmentProcessorTests
{
    private const int DefaultBatchSize = 10;
    private const int DefaultMaxRetries = 3;

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenNoPendingItems_DoesNothing()
    {
        var (processor, db) = CreateProcessor();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        (await db.DictionaryEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenPendingItem_EnrichesAndLinksEntry()
    {
        var (processor, db) = CreateProcessor();
        db.UserEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Enriched);
        updated.SourceEntryId.Should().NotBeNull();

        var dictEntry = await db.DictionaryEntries.FindAsync(updated.SourceEntryId);
        dictEntry.Should().NotBeNull();
        dictEntry!.Language.Should().Be("en");
        dictEntry.Text.Should().Be("book");
        dictEntry.BaseEntryId.Should().BeNull("base forms have no parent");
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_LinksBaseFormsViaSenseTranslations()
    {
        var (processor, db) = CreateProcessor();
        db.UserEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var sourceBase = await db.DictionaryEntries
            .Include(x => x.Senses).ThenInclude(s => s.Translations)
            .FirstAsync(x => x.Language == "en" && x.BaseEntryId == null);
        var targetBase = await db.DictionaryEntries
            .Include(x => x.Senses).ThenInclude(s => s.Translations)
            .FirstAsync(x => x.Language == "ru" && x.BaseEntryId == null);

        var targetSenseIds = targetBase.Senses.Select(s => s.Id).ToHashSet();
        sourceBase.Senses.Should().NotBeEmpty();
        sourceBase.Senses
            .SelectMany(s => s.Translations)
            .Should().Contain(t => targetSenseIds.Contains(t.TargetSenseId));
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_SetsSourceAndTargetEntryIds()
    {
        var (processor, db) = CreateProcessor();
        db.UserEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserEntries.SingleAsync();
        updated.SourceEntryId.Should().NotBeNull();
        updated.TargetEntryId.Should().NotBeNull();
        updated.SourceEntryId.Value.Should().NotBe(updated.TargetEntryId!.Value);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenExistingEntryMatches_LinksWithoutCreatingNewEntries()
    {
        var (processor, db) = CreateProcessor();
        SeedExistingTranslation(db, "book", "en", "книга", "ru");
        db.UserEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        var entriesBefore = await db.DictionaryEntries.CountAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        (await db.DictionaryEntries.CountAsync()).Should().Be(entriesBefore);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Enriched);
        updated.SourceEntryId.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenUserInputMatchesDerivedForm_ResolvesToBaseEntry()
    {
        var (processor, db) = CreateProcessor();
        var (enBase, _, ruBase) = SeedExistingTranslationWithDerivedForm(
            db, baseText: "book", derivedText: "books", sourceLang: "en",
            targetText: "книга", targetLang: "ru");
        db.UserEntries.Add(CreatePendingItem("books", "книга"));

        await db.SaveChangesAsync();
        var entriesBefore = await db.DictionaryEntries.CountAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        (await db.DictionaryEntries.CountAsync()).Should().Be(entriesBefore);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Enriched);
        updated.SourceEntryId.Should().Be(enBase.Id, "lookup must walk derived → base");
        updated.TargetEntryId.Should().Be(ruBase.Id);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_SkipsItemsWithFutureEnrichmentNotBefore()
    {
        var (processor, db) = CreateProcessor();
        var item = CreatePendingItem("book", "книга");
        item.EnrichmentNotBefore = DateTimeOffset.UtcNow.AddHours(1);
        db.UserEntries.Add(item);

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        (await db.UserEntries.SingleAsync()).EnrichmentStatus.Should().Be(EnrichmentStatus.Pending);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_RespectsBatchSize()
    {
        var (processor, db) = CreateProcessor();

        for (var i = 0; i < 5; i++)
            db.UserEntries.Add(CreatePendingItem($"word{i}", $"слово{i}"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(2, DefaultMaxRetries, CancellationToken.None);

        var enrichedCount = await db.UserEntries
            .CountAsync(x => x.EnrichmentStatus == EnrichmentStatus.Enriched);
        enrichedCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenItemHasNoTranslation_ProviderGeneratesTarget()
    {
        var (processor, db) = CreateProcessor();
        db.UserEntries.Add(CreatePendingItem("book", null));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Enriched);
        updated.SourceEntryId.Should().NotBeNull();
        updated.TargetEntryId.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenProviderThrows_IncrementsAttemptsAndSetsBackoff()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserEntry[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider failure"));

        var (processor, db) = CreateProcessor(providerMock.Object);
        db.UserEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Pending);
        updated.EnrichmentAttempts.Should().Be(1);
        updated.EnrichmentNotBefore.Should().NotBeNull().And.BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenMaxRetriesReached_MarksAsProviderError()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserEntry[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider failure"));

        var (processor, db) = CreateProcessor(providerMock.Object);
        var item = CreatePendingItem("book", "книга");
        item.EnrichmentAttempts = 2;
        db.UserEntries.Add(item);

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(DefaultBatchSize, 3, CancellationToken.None);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.ProviderError);
        updated.EnrichmentAttempts.Should().Be(3);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenProviderReturnsInvalidSource_SetsStatus()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserEntry[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntry[] items, CancellationToken _) =>
                items.Select(x => new EnrichmentResult(
                    x.Id, EnrichmentStatus.InvalidSource, null, null)).ToArray());

        var (processor, db) = CreateProcessor(providerMock.Object);
        db.UserEntries.Add(CreatePendingItem("asdfgh", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.InvalidSource);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenProviderReturnsInvalidLink_SetsStatus()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserEntry[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntry[] items, CancellationToken _) =>
                items.Select(x => new EnrichmentResult(
                    x.Id, EnrichmentStatus.InvalidLink, null, null)).ToArray());

        var (processor, db) = CreateProcessor(providerMock.Object);
        db.UserEntries.Add(CreatePendingItem("book", "кот"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.InvalidLink);
    }

    private static (EnrichmentProcessor processor, AppDbContext db) CreateProcessor(
        IEnrichmentProvider? provider = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"langoose-enrichment-unit-{Guid.NewGuid():N}")
            .Options;

        var db = new AppDbContext(options);
        var logger = NullLogger<EnrichmentProcessor>.Instance;

        return (new EnrichmentProcessor(db, provider ?? new LocalEnrichmentProvider(), logger), db);
    }

    private static UserEntry CreatePendingItem(string term, string? translation) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = Guid.CreateVersion7(),
        SourceLanguage = "en",
        TargetLanguage = "ru",
        UserInputTerm = term,
        UserInputTranslation = translation,
        PartOfSpeech = "noun",
        EnrichmentStatus = EnrichmentStatus.Pending,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static void SeedExistingTranslation(
        AppDbContext db, string sourceText, string sourceLang,
        string targetText, string targetLang)
    {
        var now = DateTimeOffset.UtcNow;
        var sourceEntry = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(), Language = sourceLang, Text = sourceText,
            PartOfSpeech = "noun", IsPublic = true,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var targetEntry = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(), Language = targetLang, Text = targetText,
            PartOfSpeech = "noun", IsPublic = true,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var sourceSense = new Sense
        {
            Id = Guid.CreateVersion7(), DictionaryEntryId = sourceEntry.Id,
            SenseIndex = 0,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var targetSense = new Sense
        {
            Id = Guid.CreateVersion7(), DictionaryEntryId = targetEntry.Id,
            SenseIndex = 0,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.DictionaryEntries.AddRange(sourceEntry, targetEntry);
        db.Senses.AddRange(sourceSense, targetSense);
        db.SenseTranslations.AddRange(
            new SenseTranslation
            {
                SourceSenseId = sourceSense.Id, TargetSenseId = targetSense.Id,
                Rank = 0, CreatedAtUtc = now
            },
            new SenseTranslation
            {
                SourceSenseId = targetSense.Id, TargetSenseId = sourceSense.Id,
                Rank = 0, CreatedAtUtc = now
            });
    }

    private static (DictionaryEntry SourceBase, DictionaryEntry SourceDerived, DictionaryEntry TargetBase)
        SeedExistingTranslationWithDerivedForm(
            AppDbContext db, string baseText, string derivedText, string sourceLang,
            string targetText, string targetLang)
    {
        var now = DateTimeOffset.UtcNow;
        var sourceBase = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(), Language = sourceLang, Text = baseText,
            PartOfSpeech = "noun", IsPublic = true,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var sourceDerived = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(), Language = sourceLang, Text = derivedText,
            BaseEntryId = sourceBase.Id,
            PartOfSpeech = "noun", IsPublic = true,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var targetBase = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(), Language = targetLang, Text = targetText,
            PartOfSpeech = "noun", IsPublic = true,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var sourceBaseSense = new Sense
        {
            Id = Guid.CreateVersion7(), DictionaryEntryId = sourceBase.Id,
            SenseIndex = 0,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var targetBaseSense = new Sense
        {
            Id = Guid.CreateVersion7(), DictionaryEntryId = targetBase.Id,
            SenseIndex = 0,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.DictionaryEntries.AddRange(sourceBase, sourceDerived, targetBase);
        db.Senses.AddRange(sourceBaseSense, targetBaseSense);
        db.SenseTranslations.AddRange(
            new SenseTranslation
            {
                SourceSenseId = sourceBaseSense.Id, TargetSenseId = targetBaseSense.Id,
                Rank = 0, CreatedAtUtc = now
            },
            new SenseTranslation
            {
                SourceSenseId = targetBaseSense.Id, TargetSenseId = sourceBaseSense.Id,
                Rank = 0, CreatedAtUtc = now
            });

        return (sourceBase, sourceDerived, targetBase);
    }
}
