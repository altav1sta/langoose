using Langoose.Api.Models;
using Langoose.Domain.Models;
using Langoose.Api.Services;
using Langoose.Api.Tests.Infrastructure;
using Xunit;

namespace Langoose.Api.Tests.Services;

public sealed class DictionaryServiceTests
{
    private const string ValidCsv =
        "English term,Russian translation(s),Type,Notes,Tags\n" +
        "improve,uluchshat,word,,study|verbs\n" +
        "take off,snimat,phrase,,phrases";

    [Fact]
    public async Task AddItemAsync_WhenAddingPhrase_PersistsPhraseAndKnownVariants()
    {
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        var item = await context.DictionaryService.AddItemAsync(
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
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        await context.DictionaryService.AddItemAsync(
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

        var before = await context.DictionaryService.GetItemsAsync(userId, CancellationToken.None);
        var beforeCustomCount = before.Count(item => item.OwnerId == userId);

        await context.DictionaryService.AddItemAsync(
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

        var after = await context.DictionaryService.GetItemsAsync(userId, CancellationToken.None);
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
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        var import = await context.DictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("words.csv", ValidCsv),
            CancellationToken.None);

        Assert.Equal(2, import.ImportedRows);

        var exported = await context.DictionaryService.ExportCsvAsync(userId, CancellationToken.None);
        Assert.Contains("improve", exported);
        Assert.Contains("take off", exported);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenRowsAlreadyExist_SkipsDuplicates()
    {
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        await context.DictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("seed.csv", ValidCsv),
            CancellationToken.None);

        var duplicateImport = await context.DictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("duplicates.csv", ValidCsv),
            CancellationToken.None);

        Assert.Equal(0, duplicateImport.ImportedRows);
        Assert.Equal(2, duplicateImport.SkippedRows);
    }

    [Fact]
    public async Task ImportCsvAsync_WhenFormattingOnlyVariantsAreUsed_MergesInsteadOfCreatingDuplicates()
    {
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        await context.DictionaryService.AddItemAsync(
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

        var duplicateImport = await context.DictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("variants.csv", variantsCsv),
            CancellationToken.None);

        Assert.Equal(0, duplicateImport.ImportedRows);

        var items = await context.DictionaryService.GetItemsAsync(userId, CancellationToken.None);
        Assert.Single(items, item =>
            item.OwnerId == userId &&
            TextNormalizer.NormalizeForComparison(item.EnglishText) == "improve");
    }

    [Fact]
    public async Task ImportCsvAsync_WhenTermsExistInBaseVocabulary_DoesNotCreateVisibleDuplicates()
    {
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();
        var csv =
            "English term,Russian translation(s),Type,Notes,Tags\n" +
            "book,kniga,word,,reading\n" +
            "at least,po krayney mere,phrase,,common";

        var import = await context.DictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("base-overlap.csv", csv),
            CancellationToken.None);

        Assert.Equal(0, import.ImportedRows);

        var items = await context.DictionaryService.GetItemsAsync(userId, CancellationToken.None);
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
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.DictionaryService.ImportCsvAsync(
                userId,
                new ImportCsvRequest("bad-header.csv", "Word,Translation,Kind\nhello,privet,word"),
                CancellationToken.None));
    }

    [Fact]
    public async Task ImportCsvAsync_WhenRowIsMalformed_DoesNotPartiallyImport()
    {
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();
        var before = await context.DictionaryService.GetItemsAsync(userId, CancellationToken.None);
        var malformedCsv =
            "English term,Russian translation(s),Type,Notes,Tags\n" +
            "new word,novoe slovo,word,,tag\n" +
            "bad row only two columns,missing";

        var result = await context.DictionaryService.ImportCsvAsync(
            userId,
            new ImportCsvRequest("malformed.csv", malformedCsv),
            CancellationToken.None);

        Assert.Equal(0, result.ImportedRows);
        Assert.Single(result.Errors);

        var after = await context.DictionaryService.GetItemsAsync(userId, CancellationToken.None);
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task ClearCustomDataAsync_RemovesUserOwnedDataButKeepsSessions()
    {
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        var item = await context.DictionaryService.AddItemAsync(
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

        await context.StudyService.SubmitAnswerAsync(
            userId,
            new StudyAnswerRequest(item.Id, "look for"),
            CancellationToken.None);

        var storeBefore = await context.DataStore.LoadAsync();
        storeBefore.SessionTokens.Add(new SessionToken
        {
            UserId = userId,
            Token = "keep-me-signed-in"
        });
        await context.DataStore.SaveAsync(storeBefore);

        await context.DictionaryService.ClearCustomDataAsync(userId, CancellationToken.None);
        var store = await context.DataStore.LoadAsync();

        Assert.DoesNotContain(store.DictionaryItems, candidate => candidate.OwnerId == userId);
        Assert.DoesNotContain(store.ReviewStates, candidate => candidate.UserId == userId);
        Assert.DoesNotContain(store.Imports, candidate => candidate.UserId == userId);
        Assert.Contains(store.SessionTokens, candidate =>
            candidate.UserId == userId &&
            candidate.Token == "keep-me-signed-in");
    }
}
