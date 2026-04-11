using Langoose.Api.IntegrationTests.Infrastructure;
using Langoose.Domain.Constants;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Xunit;

namespace Langoose.Api.IntegrationTests.Services;

public sealed class StudyServiceIntegrationTests
{
    [Fact]
    public async Task GetNextCardAsync_WhenBaseAndCustomCardsAreDue_ReturnsACard()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var dictionaryService = TestAppSetup.CreateDictionaryService(dbContext);
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();

        var customItem = await dictionaryService.AddItemAsync(userId, new AddItemInput(
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
