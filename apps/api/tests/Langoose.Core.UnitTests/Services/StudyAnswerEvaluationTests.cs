using Langoose.Core.Services;
using Langoose.Data;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Langoose.Core.UnitTests.Services;

public sealed class StudyAnswerEvaluationTests
{
    private readonly StudyService studyService = CreateStudyService();

    [Fact]
    public void EvaluateAnswer_WhenExactMatch_ReturnsCorrect()
    {
        var result = studyService.EvaluateAnswer(CreateEntry("book"), "book");

        Assert.Equal(StudyVerdict.Correct, result.Verdict);
        Assert.Equal(FeedbackCode.ExactMatch, result.FeedbackCode);
    }

    [Fact]
    public void EvaluateAnswer_WhenArticleIsAdded_ReturnsAlmostCorrect()
    {
        var result = studyService.EvaluateAnswer(CreateEntry("book"), "the book");

        Assert.Equal(StudyVerdict.AlmostCorrect, result.Verdict);
        Assert.Equal(FeedbackCode.MissingArticle, result.FeedbackCode);
    }

    [Fact]
    public void EvaluateAnswer_WhenMinorTypoIsSubmitted_ClassifiesMinorTypo()
    {
        var result = studyService.EvaluateAnswer(CreateEntry("decision"), "decison");

        Assert.Equal(FeedbackCode.MinorTypo, result.FeedbackCode);
    }

    [Fact]
    public void EvaluateAnswer_WhenCompletelyWrong_ReturnsIncorrect()
    {
        var result = studyService.EvaluateAnswer(CreateEntry("book"), "car");

        Assert.Equal(StudyVerdict.Incorrect, result.Verdict);
        Assert.Equal(FeedbackCode.MeaningMismatch, result.FeedbackCode);
    }

    private static StudyService CreateStudyService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"langoose-study-unit-{Guid.NewGuid():N}")
            .Options;

        return new StudyService(new AppDbContext(options));
    }

    private static DictionaryEntry CreateEntry(string text)
    {
        return new DictionaryEntry
        {
            Id = Guid.CreateVersion7(),
            Language = "en",
            Text = text,
            PartOfSpeech = "noun",
            IsPublic = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
