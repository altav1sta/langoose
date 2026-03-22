using System.Text.Json.Serialization;

namespace Langoose.Api.Models;

public sealed class StudyEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public DateTimeOffset AnsweredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string SubmittedAnswer { get; set; } = string.Empty;
    public string NormalizedAnswer { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StudyVerdict Verdict { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FeedbackCode FeedbackCode { get; set; }
}
