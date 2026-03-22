using System.Text.Json.Serialization;

namespace Langoose.Api.Models;

public sealed record StudyAnswerResult(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] StudyVerdict Verdict,
    string NormalizedAnswer,
    string? AcceptedVariant,
    string ExpectedAnswer,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] FeedbackCode FeedbackCode,
    DateTimeOffset NextDueAtUtc);
