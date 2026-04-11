using Langoose.Api.IntegrationTests.Infrastructure;
using Langoose.Data;
using Langoose.Data.Seeding;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Langoose.Api.IntegrationTests.Services;

public sealed class DictionaryServiceIntegrationTests
{
    private const string ValidCsv =
        "English term,Russian translation(s),Type,Notes,Tags\n" +
        "improve,uluchshat,word,,study|verbs\n" +
        "take off,snimat,phrase,,phrases";

    [Fact]
    public void SeedData_WhenLoaded_ProducesEntriesTranslationsAndContexts()
    {
        var batch = SeedDataLoader.LoadBaseItems();

        Assert.True(batch.Entries.Count > 0);
        Assert.True(batch.Translations.Count > 0);
        Assert.True(batch.Contexts.Count > 0);
        Assert.Contains(batch.Entries, e => e.Language == "en" && e.Text == "book");
        Assert.Contains(batch.Entries, e => e.Language == "ru" && e.Text == "\u043A\u043D\u0438\u0433\u0430");
    }

    [Fact]
    public async Task SeedData_WhenDataAlreadyExists_DoesNotDuplicate()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"langoose-seed-tests-{Guid.NewGuid():N}")
            .Options;
        var dbContextFactory = new TestDbContextFactory(options);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var seeder = new DatabaseSeeder(dbContext);

        await seeder.SeedAsync();
        var countBefore = await dbContext.DictionaryEntries.CountAsync();

        await seeder.SeedAsync();
        var countAfter = await dbContext.DictionaryEntries.CountAsync();

        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task AddUserEntryAsync_CreatesEntryWithPendingStatus()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        var entry = await dictionaryService.AddUserEntryAsync(
            userId,
            new AddUserEntryInput("искать", "look for", "ru", "en", Tags: ["travel"]),
            CancellationToken.None);

        Assert.Equal(EnrichmentStatus.Pending, entry.EnrichmentStatus);
        Assert.Equal("искать", entry.UserInputTerm);
        Assert.Equal("look for", entry.UserInputTranslation);
        Assert.Contains("travel", entry.Tags);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenRowsAreValid_CreatesUserEntries()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        var result = await dictionaryService.ImportCsvAsync(
            userId, ValidCsv, "words.csv", CancellationToken.None);

        Assert.Equal(2, result.RowCount);
        Assert.Equal(2, result.PendingCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenHeaderIsInvalid_ThrowsArgumentException()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            dictionaryService.ImportCsvAsync(
                userId, "Word,Translation,Kind\nhello,privet,word", "bad-header.csv",
                CancellationToken.None));
    }

    [Fact]
    public async Task ImportCsvAsync_WhenRowIsMalformed_DoesNotPartiallyImport()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();
        var malformedCsv =
            "English term,Russian translation(s),Type,Notes,Tags\n" +
            "new word,novoe slovo,word,,tag\n" +
            "bad row only two columns,missing";

        var result = await dictionaryService.ImportCsvAsync(
            userId, malformedCsv, "malformed.csv", CancellationToken.None);

        Assert.Equal(0, result.PendingCount);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ClearUserDataAsync_RemovesUserOwnedData()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        await dictionaryService.AddUserEntryAsync(
            userId,
            new AddUserEntryInput("искать", "look for", "ru", "en"),
            CancellationToken.None);

        await dictionaryService.ClearUserDataAsync(userId, CancellationToken.None);
        await using var verifyDbContext = await dbContextFactory.CreateDbContextAsync();
        var store = await TestDataSnapshot.LoadAsync(verifyDbContext);

        Assert.DoesNotContain(store.UserDictionaryEntries, e => e.UserId == userId);
        Assert.DoesNotContain(store.UserProgress, p => p.UserId == userId);
        Assert.DoesNotContain(store.ImportRecords, r => r.UserId == userId);
    }
}
