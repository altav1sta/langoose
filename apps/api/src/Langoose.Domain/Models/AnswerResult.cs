using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed record AnswerResult(
    StudyVerdict Verdict,
    string NormalizedAnswer,
    string ExpectedAnswer,
    FeedbackCode? FeedbackCode,
    DateTimeOffset NextDueAtUtc);
