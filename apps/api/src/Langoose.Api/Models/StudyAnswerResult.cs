using Langoose.Domain.Enums;

namespace Langoose.Api.Models;

public sealed record StudyAnswerResult(
    StudyVerdict Verdict,
    string NormalizedAnswer,
    string ExpectedAnswer,
    FeedbackCode? FeedbackCode,
    DateTimeOffset NextDueAtUtc);
