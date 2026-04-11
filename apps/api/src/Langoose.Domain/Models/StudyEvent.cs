using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed class StudyEvent
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public required Guid DictionaryEntryId { get; set; }
    public Guid? EntryContextId { get; set; }
    public required string UserInput { get; set; }
    public required StudyVerdict Verdict { get; set; }
    public FeedbackCode? FeedbackCode { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DictionaryEntry DictionaryEntry { get; set; } = null!;
    public EntryContext? EntryContext { get; set; }
}
