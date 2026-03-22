using Langoose.Api.Models;
using Langoose.Api.Tests.Infrastructure;
using Xunit;

namespace Langoose.Api.Tests.Services;

public sealed class StudyServiceTests
{
    [Fact]
    public async Task GetNextCardAsync_WhenBaseAndCustomCardsAreDue_ReturnsACard()
    {
        await using var context = await TestAppContext.CreateAsync();
        var userId = Guid.NewGuid();

        var customItem = await context.DictionaryService.AddItemAsync(userId, new DictionaryItemRequest(
            "look for",
            ["iskat"],
            "phrase",
            null,
            null,
            null,
            ["travel"],
            "quick-add"), CancellationToken.None);

        var store = await context.DataStore.LoadAsync();
        var baseItem = store.DictionaryItems.First(item => item.SourceType == SourceType.Base);

        var customState = store.ReviewStates.First(state => state.UserId == userId && state.ItemId == customItem.Id);
        customState.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);

        store.ReviewStates.Add(new ReviewState
        {
            UserId = userId,
            ItemId = baseItem.Id,
            DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

        await context.DataStore.SaveAsync(store);

        var card = await context.StudyService.GetNextCardAsync(userId, CancellationToken.None);

        Assert.NotNull(card);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenPhraseIsCloseVariant_ReturnsAlmostCorrect()
    {
        await using var context = await TestAppContext.CreateAsync();

        var result = context.StudyService.EvaluateAnswer(new DictionaryItem
        {
            EnglishText = "take care of",
            ItemKind = ItemKind.Phrase,
            AcceptedVariants = ["take care of"]
        }, "take care");

        Assert.Equal(StudyVerdict.AlmostCorrect, result.Verdict);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenArticleIsAdded_ReturnsAlmostCorrect()
    {
        await using var context = await TestAppContext.CreateAsync();

        var result = context.StudyService.EvaluateAnswer(new DictionaryItem
        {
            EnglishText = "book",
            ItemKind = ItemKind.Word,
            AcceptedVariants = ["book"]
        }, "the book");

        Assert.Equal(StudyVerdict.AlmostCorrect, result.Verdict);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenMinorTypoIsSubmitted_ClassifiesMinorTypo()
    {
        await using var context = await TestAppContext.CreateAsync();

        var result = context.StudyService.EvaluateAnswer(new DictionaryItem
        {
            EnglishText = "decision",
            ItemKind = ItemKind.Word,
            AcceptedVariants = ["decision"]
        }, "decison");

        Assert.Equal(FeedbackCode.MinorTypo, result.FeedbackCode);
    }

    [Fact]
    public async Task EvaluateAnswer_WhenKnownVariantIsSubmitted_ClassifiesAcceptedVariant()
    {
        await using var context = await TestAppContext.CreateAsync();

        var result = context.StudyService.EvaluateAnswer(new DictionaryItem
        {
            EnglishText = "take care of",
            ItemKind = ItemKind.Phrase,
            AcceptedVariants = ["take care of", "look after"]
        }, "look after");

        Assert.Equal(FeedbackCode.AcceptedVariant, result.FeedbackCode);
    }
}
