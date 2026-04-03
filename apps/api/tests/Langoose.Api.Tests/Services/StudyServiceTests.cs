using Langoose.Api.Models;
using Langoose.Domain.Constants;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Api.Tests.Infrastructure;
using Xunit;

namespace Langoose.Api.Tests.Services;

public sealed class StudyServiceTests
{
    [Fact]
    public async Task GetNextCardAsync_WhenBaseAndCustomCardsAreDue_ReturnsACard()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();

        var customItem = await dictionaryService.AddItemAsync(userId, new DictionaryItemRequest(
            "look for",
            ["iskat"],
            "phrase",
            null,
            null,
            null,
            ["travel"],
            "quick-add"), CancellationToken.None);

        var store = await TestDataSnapshot.LoadAsync(dbContext);
        var baseItem = store.DictionaryItems.First(x => x.SourceType == SourceType.Base);

        var customState = store.ReviewStates.First(x => x.UserId == userId && x.ItemId == customItem.Id);
        customState.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

        store.ReviewStates.Add(new ReviewState
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = baseItem.Id,
            Stability = ReviewDefaults.InitialStability,
            DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

        await dbContext.ReviewStates.AddAsync(store.ReviewStates.Last(), CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var card = await studyService.GetNextCardAsync(userId, CancellationToken.None);

        Assert.NotNull(card);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenPhraseIsCloseVariant_ReturnsAlmostCorrect()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);

        var result = studyService.EvaluateAnswer(new DictionaryItem
        {
            Id = Guid.NewGuid(),
            SourceType = SourceType.Base,
            EnglishText = "take care of",
            ItemKind = ItemKind.Phrase,
            PartOfSpeech = "phrase",
            Difficulty = "A1",
            Status = DictionaryItemStatus.Active,
            CreatedByFlow = "test",
            Notes = "",
            AcceptedVariants = ["take care of"],
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, "take care");

        Assert.Equal(StudyVerdict.AlmostCorrect, result.Verdict);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenArticleIsAdded_ReturnsAlmostCorrect()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);

        var result = studyService.EvaluateAnswer(new DictionaryItem
        {
            Id = Guid.NewGuid(),
            SourceType = SourceType.Base,
            EnglishText = "book",
            ItemKind = ItemKind.Word,
            PartOfSpeech = "noun",
            Difficulty = "A1",
            Status = DictionaryItemStatus.Active,
            CreatedByFlow = "test",
            Notes = "",
            AcceptedVariants = ["book"],
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, "the book");

        Assert.Equal(StudyVerdict.AlmostCorrect, result.Verdict);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenMinorTypoIsSubmitted_ClassifiesMinorTypo()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);

        var result = studyService.EvaluateAnswer(new DictionaryItem
        {
            Id = Guid.NewGuid(),
            SourceType = SourceType.Base,
            EnglishText = "decision",
            ItemKind = ItemKind.Word,
            PartOfSpeech = "noun",
            Difficulty = "A1",
            Status = DictionaryItemStatus.Active,
            CreatedByFlow = "test",
            Notes = "",
            AcceptedVariants = ["decision"],
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, "decison");

        Assert.Equal(FeedbackCode.MinorTypo, result.FeedbackCode);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenKnownVariantIsSubmitted_ClassifiesAcceptedVariant()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);

        var result = studyService.EvaluateAnswer(new DictionaryItem
        {
            Id = Guid.NewGuid(),
            SourceType = SourceType.Base,
            EnglishText = "take care of",
            ItemKind = ItemKind.Phrase,
            PartOfSpeech = "phrase",
            Difficulty = "A1",
            Status = DictionaryItemStatus.Active,
            CreatedByFlow = "test",
            Notes = "",
            AcceptedVariants = ["take care of", "look after"],
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, "look after");

        Assert.Equal(FeedbackCode.AcceptedVariant, result.FeedbackCode);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsOnlyTodaysVisibleStudyEvents()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();
        var item = (await TestDataSnapshot.LoadAsync(dbContext)).DictionaryItems.First(x => x.SourceType == SourceType.Base);
        var startOfTodayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;

        dbContext.StudyEvents.AddRange(
            new StudyEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ItemId = item.Id,
                AnsweredAtUtc = startOfTodayUtc.AddHours(1),
                SubmittedAnswer = "one",
                NormalizedAnswer = "one",
                Verdict = StudyVerdict.Correct,
                FeedbackCode = FeedbackCode.ExactMatch
            },
            new StudyEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ItemId = item.Id,
                AnsweredAtUtc = startOfTodayUtc.AddDays(-1).AddHours(23),
                SubmittedAnswer = "two",
                NormalizedAnswer = "two",
                Verdict = StudyVerdict.Correct,
                FeedbackCode = FeedbackCode.ExactMatch
            });

        await dbContext.SaveChangesAsync(CancellationToken.None);

        var dashboard = await studyService.GetDashboardAsync(userId, CancellationToken.None);

        Assert.Equal(1, dashboard.StudiedToday);
    }
}
