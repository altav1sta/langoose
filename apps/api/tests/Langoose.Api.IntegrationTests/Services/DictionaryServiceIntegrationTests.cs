using Langoose.Api.Models;
using Langoose.Api.Services;
using Langoose.Api.IntegrationTests.Infrastructure;
using Langoose.Data;
using Langoose.Data.Seeding;
using Langoose.Domain.Enums;
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
    public void SeedData_WhenLoaded_PreservesRussianGlossesAndHints()
    {
        var seedItems = SeedDataLoader.LoadBaseItems();

        Assert.Contains(seedItems, pair => pair.Item.EnglishText == "book" &&
            pair.Item.RussianGlosses.Contains("книга") &&
            pair.Sentence.TranslationHint == "Я читаю книгу перед сном.");
        Assert.DoesNotContain(seedItems, pair =>
            pair.Item.RussianGlosses.Any(gloss => gloss.Contains('?')) ||
            pair.Sentence.TranslationHint.Contains('?'));
    }

    [Fact]
    public async Task SeedData_WhenDataAlreadyExists_DoesNotRewriteExistingValues()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"langoose-seed-tests-{Guid.NewGuid():N}")
            .Options;
        var dbContextFactory = new TestDbContextFactory(options);
        await using var seededDbContext = await dbContextFactory.CreateDbContextAsync();
        var seeder = new DatabaseSeeder(seededDbContext);

        await seeder.SeedAsync();

        var store = await TestDataSnapshot.LoadAsync(seededDbContext);
        var baseItem = Assert.Single(store.DictionaryItems, item =>
            item.SourceType == SourceType.Base &&
            item.EnglishText == "book");

        baseItem.AcceptedVariants = ["book", "volume"];
        baseItem.Distractors = ["alpha"];

        await seededDbContext.SaveChangesAsync();

        await seeder.SeedAsync();

        await using var unchangedDbContext = await dbContextFactory.CreateDbContextAsync();
        var unchanged = await TestDataSnapshot.LoadAsync(unchangedDbContext);
        var unchangedItem = Assert.Single(unchanged.DictionaryItems, item =>
            item.SourceType == SourceType.Base &&
            item.EnglishText == "book");

        Assert.Equal(["book", "volume"], unchangedItem.AcceptedVariants);
        Assert.Equal(["alpha"], unchangedItem.Distractors);
    }

    [Fact]
    public async Task AddItemAsync_WhenAddingPhrase_PersistsPhraseAndKnownVariants()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        var item = await dictionaryService.AddItemAsync(
            userId,
            new DictionaryItemRequest(
                "look for",
                ["iskat"],
                "phrase",
                null,
                null,
                null,
                ["travel"],
                "quick-add"),
            CancellationToken.None);

        Assert.Equal(ItemKind.Phrase, item.ItemKind);
        Assert.Contains("iskat", item.RussianGlosses);
        Assert.Contains("search for", item.AcceptedVariants);
    }

    [Fact]
    public async Task AddItemAsync_WhenAddingDuplicateQuickAdd_MergesIntoExistingCustomItem()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        await dictionaryService.AddItemAsync(
            userId,
            new DictionaryItemRequest(
                "look for",
                ["iskat"],
                "phrase",
                null,
                null,
                null,
                ["travel"],
                "quick-add"),
            CancellationToken.None);

        var before = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
        var beforeCustomCount = before.Count(item => item.OwnerId == userId);

        await dictionaryService.AddItemAsync(
            userId,
            new DictionaryItemRequest(
                " look for ",
                ["razyskivat"],
                "phrase",
                null,
                null,
                "extra note",
                ["travel"],
                "quick-add"),
            CancellationToken.None);

        var after = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
        var afterCustomCount = after.Count(item => item.OwnerId == userId);
        var merged = Assert.Single(after, item =>
            item.OwnerId == userId &&
            TextNormalizer.NormalizeForComparison(item.EnglishText) == "look for");

        Assert.Equal(beforeCustomCount, afterCustomCount);
        Assert.Contains("razyskivat", merged.RussianGlosses);
        Assert.Contains("extra note", merged.Notes);
    }

    [Fact]
    public async Task ImportAndExportCsv_WhenRowsAreValid_RoundTripsCustomEntries()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        var import = await dictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("words.csv", ValidCsv),
            CancellationToken.None);

        Assert.Equal(2, import.ImportedRows);

        var exported = await dictionaryService.ExportCsvAsync(userId, CancellationToken.None);
        Assert.Contains("improve", exported);
        Assert.Contains("take off", exported);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenRowsAlreadyExist_SkipsDuplicates()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        await dictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("seed.csv", ValidCsv),
            CancellationToken.None);

        var duplicateImport = await dictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("duplicates.csv", ValidCsv),
            CancellationToken.None);

        Assert.Equal(0, duplicateImport.ImportedRows);
        Assert.Equal(2, duplicateImport.SkippedRows);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenFormattingOnlyVariantsAreUsed_MergesInsteadOfCreatingDuplicates()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();

        await dictionaryService.AddItemAsync(
            userId,
            new DictionaryItemRequest(
                "improve",
                ["uluchshat"],
                "word",
                null,
                null,
                null,
                ["verbs"],
                "quick-add"),
            CancellationToken.None);

        var variantsCsv =
            "English term,Russian translation(s),Type,Notes,Tags\n" +
            "\uFEFF\"Improve\",uluchshat,word,,verbs\n" +
            " improve ,stanovitsya luchshe,,,";

        var duplicateImport = await dictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("variants.csv", variantsCsv),
            CancellationToken.None);

        Assert.Equal(0, duplicateImport.ImportedRows);

        var items = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
        Assert.Single(items, item =>
            item.OwnerId == userId &&
            TextNormalizer.NormalizeForComparison(item.EnglishText) == "improve");
    }

    [Fact]
    public async Task ImportCsvAsync_WhenTermsExistInBaseVocabulary_DoesNotCreateVisibleDuplicates()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();
        var csv =
            "English term,Russian translation(s),Type,Notes,Tags\n" +
            "book,kniga,word,,reading\n" +
            "at least,po krayney mere,phrase,,common";

        var import = await dictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("base-overlap.csv", csv),
            CancellationToken.None);

        Assert.Equal(0, import.ImportedRows);

        var items = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
        Assert.Single(
            items,
            item => TextNormalizer.NormalizeForComparison(item.EnglishText) == "book");
        Assert.Single(
            items,
            item => TextNormalizer.NormalizeForComparison(item.EnglishText) == "at least");
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
                userId,
                new ImportCsvRequest("bad-header.csv", "Word,Translation,Kind\nhello,privet,word"),
                CancellationToken.None));
    }

    [Fact]
    public async Task ImportCsvAsync_WhenRowIsMalformed_DoesNotPartiallyImport()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var userId = Guid.NewGuid();
        var before = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
        var malformedCsv =
            "English term,Russian translation(s),Type,Notes,Tags\n" +
            "new word,novoe slovo,word,,tag\n" +
            "bad row only two columns,missing";

        var result = await dictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("malformed.csv", malformedCsv),
            CancellationToken.None);

        Assert.Equal(0, result.ImportedRows);
        Assert.Single(result.Errors);

        var after = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task ClearCustomDataAsync_RemovesUserOwnedData()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();

        var item = await dictionaryService.AddItemAsync(
            userId,
            new DictionaryItemRequest(
                "look for",
                ["iskat"],
                "phrase",
                null,
                null,
                null,
                ["travel"],
                "quick-add"),
            CancellationToken.None);

        await studyService.SubmitAnswerAsync(
            userId,
            new StudyAnswerRequest(item.Id, "look for"),
            CancellationToken.None);

        await dictionaryService.ClearCustomDataAsync(userId, CancellationToken.None);
        await using var verifyDbContext = await dbContextFactory.CreateDbContextAsync();
        var store = await TestDataSnapshot.LoadAsync(verifyDbContext);

        Assert.DoesNotContain(store.DictionaryItems, candidate => candidate.OwnerId == userId);
        Assert.DoesNotContain(store.ReviewStates, candidate => candidate.UserId == userId);
        Assert.DoesNotContain(store.Imports, candidate => candidate.UserId == userId);
    }
}
