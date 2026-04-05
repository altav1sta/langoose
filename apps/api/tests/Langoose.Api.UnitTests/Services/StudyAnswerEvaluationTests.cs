using Langoose.Api.Models;
using Langoose.Api.Services;
using Langoose.Data;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Langoose.Api.UnitTests.Services;

public sealed class StudyAnswerEvaluationTests
{
    private readonly StudyService studyService = CreateStudyService();

    [Fact]
    public void EvaluateAnswer_WhenPhraseIsCloseVariant_ReturnsAlmostCorrect()
    {
        var result = studyService.EvaluateAnswer(CreateItem("take care of", ItemKind.Phrase), "take care");

        Assert.Equal(StudyVerdict.AlmostCorrect, result.Verdict);
    }

    [Fact]
    public void EvaluateAnswer_WhenArticleIsAdded_ReturnsAlmostCorrect()
    {
        var result = studyService.EvaluateAnswer(CreateItem("book", ItemKind.Word), "the book");

        Assert.Equal(StudyVerdict.AlmostCorrect, result.Verdict);
    }

    [Fact]
    public void EvaluateAnswer_WhenMinorTypoIsSubmitted_ClassifiesMinorTypo()
    {
        var result = studyService.EvaluateAnswer(CreateItem("decision", ItemKind.Word), "decison");

        Assert.Equal(FeedbackCode.MinorTypo, result.FeedbackCode);
    }

    [Fact]
    public void EvaluateAnswer_WhenKnownVariantIsSubmitted_ClassifiesAcceptedVariant()
    {
        var result = studyService.EvaluateAnswer(
            CreateItem("take care of", ItemKind.Phrase, "look after"),
            "look after");

        Assert.Equal(FeedbackCode.AcceptedVariant, result.FeedbackCode);
    }

    private static StudyService CreateStudyService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"langoose-study-unit-{Guid.NewGuid():N}")
            .Options;

        return new StudyService(new AppDbContext(options));
    }

    private static DictionaryItem CreateItem(string englishText, ItemKind itemKind, params string[] acceptedVariants)
    {
        return new DictionaryItem
        {
            Id = Guid.NewGuid(),
            SourceType = SourceType.Base,
            EnglishText = englishText,
            ItemKind = itemKind,
            PartOfSpeech = itemKind == ItemKind.Phrase ? "phrase" : "noun",
            Difficulty = "A1",
            Status = DictionaryItemStatus.Active,
            CreatedByFlow = "test",
            Notes = "",
            AcceptedVariants = [englishText, .. acceptedVariants],
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
