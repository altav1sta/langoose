using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed class StudyEvent
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public required Guid ItemId { get; set; }
    public required DateTimeOffset AnsweredAtUtc { get; set; }
    public required string SubmittedAnswer { get; set; }
    public required string NormalizedAnswer { get; set; }
    public required StudyVerdict Verdict { get; set; }
    public required FeedbackCode FeedbackCode { get; set; }
}
