using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed record AnswerResult(
    StudyVerdict Verdict,
    string NormalizedAnswer,
    string? AcceptedVariant,
    string ExpectedAnswer,
    FeedbackCode FeedbackCode,
    DateTimeOffset NextDueAtUtc);
