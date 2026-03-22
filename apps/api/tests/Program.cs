using Langoose.Api.Infrastructure;
using Langoose.Api.Models;
using Langoose.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

var root = Path.Combine(Path.GetTempPath(), "langoose-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:DataDirectory"] = root
        })
        .Build();

    var environment = new StubEnvironment(root);
    var dataStore = new FileDataStore(configuration, environment);
    var seeder = new DataSeeder(dataStore);
    await seeder.SeedAsync();

    var enrichmentService = new EnrichmentService();
    var dictionaryService = new DictionaryService(dataStore, enrichmentService);
    var studyService = new StudyService(dataStore);

    var userId = Guid.NewGuid();

    await ShouldQuickAddPhrase(dictionaryService, userId);
    await ShouldMergeDuplicateQuickAdds(dictionaryService, userId);
    await ShouldRoundTripCsv(dictionaryService, userId);
    await ShouldSkipDuplicateCsvRows(dictionaryService, userId);
    await ShouldNormalizeDuplicateCsvVariants(dictionaryService, userId);
    await ShouldSkipBaseVocabularyDuplicates(dictionaryService, userId);
    await ShouldRejectInvalidCsvHeader(dictionaryService, userId);
    await ShouldRejectMalformedCsvWithoutPartialImport(dictionaryService, userId);
    await ShouldReturnMixedStudyCards(dataStore, studyService, userId);
    ShouldAcceptNearVariants(studyService);
    ShouldAcceptKnownVariants(studyService);
    ShouldValidateBadEnrichment(enrichmentService);
    await ShouldClearCustomData(dictionaryService, dataStore, userId);

    Console.WriteLine("All Langoose MVP checks passed.");
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task ShouldQuickAddPhrase(DictionaryService dictionaryService, Guid userId)
{
    var item = await dictionaryService.AddItemAsync(userId, new DictionaryItemRequest(
        "look for",
        ["iskat"],
        "phrase",
        null,
        null,
        null,
        ["travel"],
        "quick-add"), CancellationToken.None);

    Assert(item.ItemKind == ItemKind.Phrase, "Custom quick add should persist phrases.");
    Assert(item.RussianGlosses.Contains("iskat"), "Custom item should keep glosses.");
    Assert(item.AcceptedVariants.Contains("search for"), "Lexicon-backed quick add should keep known variants.");
}

static async Task ShouldMergeDuplicateQuickAdds(DictionaryService dictionaryService, Guid userId)
{
    var before = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
    var beforeCustomCount = before.Count(item => item.OwnerId == userId);

    await dictionaryService.AddItemAsync(userId, new DictionaryItemRequest(
        " look for ",
        ["razyskivat"],
        "phrase",
        null,
        null,
        "extra note",
        ["travel"],
        "quick-add"), CancellationToken.None);

    var after = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
    var afterCustomCount = after.Count(item => item.OwnerId == userId);
    var merged = after.Single(item => item.OwnerId == userId && TextNormalizer.NormalizeForComparison(item.EnglishText) == "look for");

    Assert(afterCustomCount == beforeCustomCount, "Duplicate quick add should merge into the existing custom item.");
    Assert(merged.RussianGlosses.Contains("razyskivat"), "Merged duplicate should keep new glosses.");
    Assert(merged.Notes.Contains("extra note"), "Merged duplicate should preserve additional notes.");
}

static async Task ShouldRoundTripCsv(DictionaryService dictionaryService, Guid userId)
{
    var import = await dictionaryService.ImportCsvAsync(userId, new ImportCsvRequest(
        "words.csv",
        "English term,Russian translation(s),Type,Notes,Tags\nimprove,uluchshat,word,,study|verbs\nat least,po krayney mere,phrase,,phrases"
    ), CancellationToken.None);

    Assert(import.ImportedRows == 2, "CSV import should load both valid rows.");
    var exported = await dictionaryService.ExportCsvAsync(userId, CancellationToken.None);
    Assert(exported.Contains("improve"), "CSV export should include imported words.");
    Assert(exported.Contains("at least"), "CSV export should include imported phrases.");
}

static async Task ShouldSkipDuplicateCsvRows(DictionaryService dictionaryService, Guid userId)
{
    var duplicateImport = await dictionaryService.ImportCsvAsync(userId, new ImportCsvRequest(
        "duplicates.csv",
        "English term,Russian translation(s),Type,Notes,Tags\nimprove,uluchshat,word,,study|verbs\nat least,po krayney mere,phrase,,phrases"
    ), CancellationToken.None);

    Assert(duplicateImport.ImportedRows == 0, "Re-importing the same CSV rows should not create duplicates.");
    Assert(duplicateImport.SkippedRows == 2, "Duplicate CSV rows should be reported as skipped.");
}

static async Task ShouldNormalizeDuplicateCsvVariants(DictionaryService dictionaryService, Guid userId)
{
    var duplicateImport = await dictionaryService.ImportCsvAsync(userId, new ImportCsvRequest(
        "variants.csv",
        "English term,Russian translation(s),Type,Notes,Tags\n\uFEFF\"Improve\",uluchshat,word,,verbs\n improve ,stanovitsya luchshe,,,")
    , CancellationToken.None);

    Assert(duplicateImport.ImportedRows == 0, "Formatting-only CSV variants should merge instead of creating duplicates.");

    var items = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
    Assert(items.Count(item => item.OwnerId == userId && TextNormalizer.NormalizeForComparison(item.EnglishText) == "improve") == 1,
        "Normalized import variants should leave only one custom entry for the same word.");
}

static async Task ShouldSkipBaseVocabularyDuplicates(DictionaryService dictionaryService, Guid userId)
{
    var import = await dictionaryService.ImportCsvAsync(userId, new ImportCsvRequest(
        "base-overlap.csv",
        "English term,Russian translation(s),Type,Notes,Tags\nbook,kniga,word,,reading\nat least,po krayney mere,phrase,,common"
    ), CancellationToken.None);

    Assert(import.ImportedRows == 0, "Importing terms that already exist in base vocabulary should not create visible duplicates.");

    var items = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
    Assert(items.Count(item => TextNormalizer.NormalizeForComparison(item.EnglishText) == "book") == 1,
        "Visible dictionary items should collapse base/custom duplicates for the same term.");
    Assert(items.Count(item => TextNormalizer.NormalizeForComparison(item.EnglishText) == "at least") == 1,
        "Visible dictionary items should collapse duplicate phrases against the base set.");
}

static async Task ShouldRejectInvalidCsvHeader(DictionaryService dictionaryService, Guid userId)
{
    var threw = false;
    try
    {
        await dictionaryService.ImportCsvAsync(userId, new ImportCsvRequest(
            "bad-header.csv",
            "Word,Translation,Kind\nhello,privet,word"
        ), CancellationToken.None);
    }
    catch (ArgumentException)
    {
        threw = true;
    }

    Assert(threw, "Import should reject CSV files with unexpected headers.");
}

static async Task ShouldRejectMalformedCsvWithoutPartialImport(DictionaryService dictionaryService, Guid userId)
{
    var before = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
    var beforeVisibleCount = before.Count;

    var result = await dictionaryService.ImportCsvAsync(userId, new ImportCsvRequest(
        "malformed.csv",
        "English term,Russian translation(s),Type,Notes,Tags\nnew word,novoe slovo,word,,tag\nbad row only two columns,missing"
    ), CancellationToken.None);

    Assert(result.ImportedRows == 0, "Malformed CSV should not partially import rows before reporting errors.");
    Assert(result.Errors.Count == 1, "Malformed CSV should report the invalid row.");

    var after = await dictionaryService.GetItemsAsync(userId, CancellationToken.None);
    Assert(after.Count == beforeVisibleCount, "Malformed CSV should leave dictionary contents unchanged.");
}

static async Task ShouldReturnMixedStudyCards(FileDataStore dataStore, StudyService studyService, Guid userId)
{
    var store = await dataStore.LoadAsync();
    var custom = store.DictionaryItems.First(item => item.OwnerId == userId || item.SourceType == SourceType.Custom);
    var baseItem = store.DictionaryItems.First(item => item.SourceType == SourceType.Base);

    var customState = store.ReviewStates.First(state => state.UserId == userId && state.ItemId == custom.Id);
    customState.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
    var baseState = store.ReviewStates.FirstOrDefault(state => state.UserId == userId && state.ItemId == baseItem.Id);
    if (baseState is null)
    {
        store.ReviewStates.Add(new ReviewState
        {
            UserId = userId,
            ItemId = baseItem.Id,
            DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
    }
    else
    {
        baseState.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
    }

    await dataStore.SaveAsync(store);
    var card = await studyService.GetNextCardAsync(userId, CancellationToken.None);
    Assert(card is not null, "Study service should return a due card.");
}

static void ShouldAcceptNearVariants(StudyService studyService)
{
    var result = studyService.EvaluateAnswer(new DictionaryItem
    {
        EnglishText = "take care of",
        ItemKind = ItemKind.Phrase,
        AcceptedVariants = ["take care of"]
    }, "take care");

    Assert(result.Verdict == StudyVerdict.AlmostCorrect, "Phrase scoring should allow close variants.");

    var articleResult = studyService.EvaluateAnswer(new DictionaryItem
    {
        EnglishText = "book",
        ItemKind = ItemKind.Word,
        AcceptedVariants = ["book"]
    }, "the book");

    Assert(articleResult.Verdict == StudyVerdict.AlmostCorrect, "Missing articles should be tolerated.");

    var typoResult = studyService.EvaluateAnswer(new DictionaryItem
    {
        EnglishText = "decision",
        ItemKind = ItemKind.Word,
        AcceptedVariants = ["decision"]
    }, "decison");

    Assert(typoResult.FeedbackCode == FeedbackCode.MinorTypo, "Minor spelling mistakes should be classified separately.");
}

static void ShouldAcceptKnownVariants(StudyService studyService)
{
    var variantResult = studyService.EvaluateAnswer(new DictionaryItem
    {
        EnglishText = "take care of",
        ItemKind = ItemKind.Phrase,
        AcceptedVariants = ["take care of", "look after"]
    }, "look after");

    Assert(variantResult.FeedbackCode == FeedbackCode.AcceptedVariant, "Known variants should not be treated as meaning mismatches.");
}

static void ShouldValidateBadEnrichment(EnrichmentService enrichmentService)
{
    var response = enrichmentService.Enrich(new EnrichmentRequest("mysterious word", ["english gloss"], "phrase"));
    Assert(response.ValidationWarnings.Count > 0, "Bad enrichment should surface validation warnings.");
}

static async Task ShouldClearCustomData(DictionaryService dictionaryService, FileDataStore dataStore, Guid userId)
{
    var storeBefore = await dataStore.LoadAsync();
    storeBefore.SessionTokens.Add(new SessionToken { UserId = userId, Token = "keep-me-signed-in" });
    await dataStore.SaveAsync(storeBefore);

    await dictionaryService.ClearCustomDataAsync(userId, CancellationToken.None);
    var store = await dataStore.LoadAsync();

    Assert(store.DictionaryItems.All(item => item.OwnerId != userId), "Clearing custom data should remove the user's custom items.");
    Assert(store.ReviewStates.All(state => state.UserId != userId), "Clearing custom data should remove the user's review state.");
    Assert(store.Imports.All(importRecord => importRecord.UserId != userId), "Clearing custom data should remove the user's import history.");
    Assert(store.SessionTokens.Any(session => session.UserId == userId && session.Token == "keep-me-signed-in"), "Clearing custom data should not revoke active sessions.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class StubEnvironment : IWebHostEnvironment
{
    public StubEnvironment(string root)
    {
        ApplicationName = "Langoose.Api.Tests";
        WebRootPath = root;
        WebRootFileProvider = new NullFileProvider();
        EnvironmentName = "Development";
        ContentRootPath = root;
        ContentRootFileProvider = new NullFileProvider();
    }

    public string ApplicationName { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}
