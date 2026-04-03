using Langoose.Domain.Enums;

namespace Langoose.Api.Models;

public sealed record StudyAnswerResult(
    StudyVerdict Verdict,
    string NormalizedAnswer,
    string? AcceptedVariant,
    string ExpectedAnswer,
    FeedbackCode FeedbackCode,
    DateTimeOffset NextDueAtUtc);
