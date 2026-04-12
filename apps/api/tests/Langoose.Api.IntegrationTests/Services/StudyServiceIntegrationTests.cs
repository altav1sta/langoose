using Langoose.Api.IntegrationTests.Infrastructure;
using Langoose.Domain.Constants;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Xunit;

namespace Langoose.Api.IntegrationTests.Services;

public sealed class StudyServiceIntegrationTests
{
    [Fact]
    public async Task GetNextCardAsync_WhenProgressExists_ReturnsACard()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();

        var store = await TestDataSnapshot.LoadAsync(dbContext);
        var entry = store.DictionaryEntries.First(e => e.Language == "en" && e.IsPublic);

        dbContext.UserProgress.Add(new UserProgress
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DictionaryEntryId = entry.Id,
            DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Stability = ProgressDefaults.InitialStability,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var card = await studyService.GetNextCardAsync(userId, CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal(entry.Id, card.DictionaryEntryId);
    }

    [Fact]
    public async Task GetNextCardAsync_ReturnsTranslationsAndGrammarHint()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();

        var store = await TestDataSnapshot.LoadAsync(dbContext);
        var entry = store.DictionaryEntries.First(e => e.Language == "en" && e.IsPublic);

        dbContext.UserProgress.Add(new UserProgress
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DictionaryEntryId = entry.Id,
            DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Stability = ProgressDefaults.InitialStability,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var card = await studyService.GetNextCardAsync(userId, CancellationToken.None);

        Assert.NotNull(card);
        Assert.NotNull(card.EntryContextId);
        Assert.NotEmpty(card.Translations);
        Assert.Null(card.GrammarHint);
        Assert.NotEmpty(card.SentenceTranslation);
    }

    [Fact]
    public async Task SubmitAnswerAsync_RecordsEntryContextId()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();

        var store = await TestDataSnapshot.LoadAsync(dbContext);
        var entry = store.DictionaryEntries.First(e => e.Language == "en" && e.IsPublic);
        var context = store.EntryContexts.First(c => c.DictionaryEntryId == entry.Id);

        var result = await studyService.SubmitAnswerAsync(
            userId, entry.Id, context.Id, entry.Text, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(StudyVerdict.Correct, result.Verdict);

        var studyEvent = dbContext.StudyEvents.First(e => e.UserId == userId);
        Assert.Equal(context.Id, studyEvent.EntryContextId);
    }

    [Fact]
    public async Task SubmitAnswerAsync_IgnoresMismatchedEntryContextId()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();

        var store = await TestDataSnapshot.LoadAsync(dbContext);
        var entry = store.DictionaryEntries.First(e => e.Language == "en" && e.IsPublic);
        var otherEntry = store.DictionaryEntries.First(e => e.Language == "ru" && e.IsPublic);
        var wrongContext = store.EntryContexts.First(c => c.DictionaryEntryId == otherEntry.Id);

        var result = await studyService.SubmitAnswerAsync(
            userId, entry.Id, wrongContext.Id, entry.Text, CancellationToken.None);

        Assert.NotNull(result);

        var studyEvent = dbContext.StudyEvents.First(e => e.UserId == userId);
        Assert.Null(studyEvent.EntryContextId);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsOnlyTodaysStudyEvents()
    {
        var dbContextFactory = await TestAppSetup.CreateSeededDbContextFactoryAsync();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var studyService = TestAppSetup.CreateStudyService(dbContext);
        var userId = Guid.NewGuid();
        var entry = (await TestDataSnapshot.LoadAsync(dbContext)).DictionaryEntries
            .First(e => e.Language == "en" && e.IsPublic);
        var startOfTodayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;

        dbContext.StudyEvents.AddRange(
            new StudyEvent
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                DictionaryEntryId = entry.Id,
                UserInput = "one",
                Verdict = StudyVerdict.Correct,
                FeedbackCode = FeedbackCode.ExactMatch,
                CreatedAtUtc = startOfTodayUtc.AddHours(1)
            },
            new StudyEvent
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                DictionaryEntryId = entry.Id,
                UserInput = "two",
                Verdict = StudyVerdict.Correct,
                FeedbackCode = FeedbackCode.ExactMatch,
                CreatedAtUtc = startOfTodayUtc.AddDays(-1).AddHours(23)
            });

        await dbContext.SaveChangesAsync(CancellationToken.None);

        var dashboard = await studyService.GetDashboardAsync(userId, CancellationToken.None);

        Assert.Equal(1, dashboard.StudiedToday);
    }
}
