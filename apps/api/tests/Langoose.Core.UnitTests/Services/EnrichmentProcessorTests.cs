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
        db.UserDictionaryEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserDictionaryEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Enriched);
        updated.SourceEntryId.Should().NotBeNull();

        var dictEntry = await db.DictionaryEntries.FindAsync(updated.SourceEntryId);
        dictEntry.Should().NotBeNull();
        dictEntry!.Language.Should().Be("en");
        dictEntry.Text.Should().Be("book");
        dictEntry.BaseEntryId.Should().BeNull("base forms have no parent");
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_LinksBaseFormsViaManyToMany()
    {
        var (processor, db) = CreateProcessor();
        db.UserDictionaryEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var sourceBase = await db.DictionaryEntries
            .Include(x => x.Translations)
            .FirstAsync(x => x.Language == "en" && x.BaseEntryId == null);
        var targetBase = await db.DictionaryEntries
            .FirstAsync(x => x.Language == "ru" && x.BaseEntryId == null);

        sourceBase.Translations.Should().Contain(x => x.Id == targetBase.Id);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_SetsSourceAndTargetEntryIds()
    {
        var (processor, db) = CreateProcessor();
        db.UserDictionaryEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserDictionaryEntries.SingleAsync();
        updated.SourceEntryId.Should().NotBeNull();
        updated.TargetEntryId.Should().NotBeNull();
        updated.SourceEntryId.Value.Should().NotBe(updated.TargetEntryId!.Value);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenExistingEntryMatches_LinksWithoutCreatingNewEntries()
    {
        var (processor, db) = CreateProcessor();
        SeedExistingTranslation(db, "book", "en", "книга", "ru");
        db.UserDictionaryEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        var entriesBefore = await db.DictionaryEntries.CountAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        (await db.DictionaryEntries.CountAsync()).Should().Be(entriesBefore);

        var updated = await db.UserDictionaryEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Enriched);
        updated.SourceEntryId.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_SkipsItemsWithFutureEnrichmentNotBefore()
    {
        var (processor, db) = CreateProcessor();
        var item = CreatePendingItem("book", "книга");
        item.EnrichmentNotBefore = DateTimeOffset.UtcNow.AddHours(1);
        db.UserDictionaryEntries.Add(item);

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        (await db.UserDictionaryEntries.SingleAsync()).EnrichmentStatus.Should().Be(EnrichmentStatus.Pending);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_RespectsBatchSize()
    {
        var (processor, db) = CreateProcessor();

        for (var i = 0; i < 5; i++)
            db.UserDictionaryEntries.Add(CreatePendingItem($"word{i}", $"слово{i}"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(2, DefaultMaxRetries, CancellationToken.None);

        var enrichedCount = await db.UserDictionaryEntries
            .CountAsync(x => x.EnrichmentStatus == EnrichmentStatus.Enriched);
        enrichedCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenItemHasNoTranslation_ProviderGeneratesTarget()
    {
        var (processor, db) = CreateProcessor();
        db.UserDictionaryEntries.Add(CreatePendingItem("book", null));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserDictionaryEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Enriched);
        updated.SourceEntryId.Should().NotBeNull();
        updated.TargetEntryId.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenProviderThrows_IncrementsAttemptsAndSetsBackoff()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserDictionaryEntry[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider failure"));

        var (processor, db) = CreateProcessor(providerMock.Object);
        db.UserDictionaryEntries.Add(CreatePendingItem("book", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserDictionaryEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.Pending);
        updated.EnrichmentAttempts.Should().Be(1);
        updated.EnrichmentNotBefore.Should().NotBeNull().And.BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenMaxRetriesReached_MarksAsProviderError()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserDictionaryEntry[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider failure"));

        var (processor, db) = CreateProcessor(providerMock.Object);
        var item = CreatePendingItem("book", "книга");
        item.EnrichmentAttempts = 2;
        db.UserDictionaryEntries.Add(item);

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(DefaultBatchSize, 3, CancellationToken.None);

        var updated = await db.UserDictionaryEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.ProviderError);
        updated.EnrichmentAttempts.Should().Be(3);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenProviderReturnsInvalidSource_SetsStatus()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserDictionaryEntry[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDictionaryEntry[] items, CancellationToken _) =>
                items.Select(x => new EnrichmentResult(
                    x.Id, EnrichmentStatus.InvalidSource, null, null)).ToArray());

        var (processor, db) = CreateProcessor(providerMock.Object);
        db.UserDictionaryEntries.Add(CreatePendingItem("asdfgh", "книга"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserDictionaryEntries.SingleAsync();
        updated.EnrichmentStatus.Should().Be(EnrichmentStatus.InvalidSource);
    }

    [Fact]
    public async Task ProcessPendingBatchAsync_WhenProviderReturnsInvalidLink_SetsStatus()
    {
        var providerMock = new Mock<IEnrichmentProvider>();
        providerMock
            .Setup(x => x.EnrichBatchAsync(It.IsAny<UserDictionaryEntry[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDictionaryEntry[] items, CancellationToken _) =>
                items.Select(x => new EnrichmentResult(
                    x.Id, EnrichmentStatus.InvalidLink, null, null)).ToArray());

        var (processor, db) = CreateProcessor(providerMock.Object);
        db.UserDictionaryEntries.Add(CreatePendingItem("book", "кот"));

        await db.SaveChangesAsync();

        await processor.ProcessPendingBatchAsync(
            DefaultBatchSize, DefaultMaxRetries, CancellationToken.None);

        var updated = await db.UserDictionaryEntries.SingleAsync();
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

    private static UserDictionaryEntry CreatePendingItem(string term, string? translation) => new()
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
        var sourceEntry = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(), Language = sourceLang, Text = sourceText,
            PartOfSpeech = "noun", IsPublic = true,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var targetEntry = new DictionaryEntry
        {
            Id = Guid.CreateVersion7(), Language = targetLang, Text = targetText,
            PartOfSpeech = "noun", IsPublic = true,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.DictionaryEntries.AddRange(sourceEntry, targetEntry);
        sourceEntry.Translations.Add(targetEntry);
        targetEntry.Translations.Add(sourceEntry);
    }
}
