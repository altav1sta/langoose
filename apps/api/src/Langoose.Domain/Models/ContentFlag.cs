namespace Langoose.Domain.Models;

public sealed class ContentFlag
{
    public required Guid Id { get; init; }
    public required Guid DictionaryEntryId { get; set; }
    public required string Reason { get; set; }
    public Guid? ReportedByUserId { get; set; }
    public bool IsResolved { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DictionaryEntry DictionaryEntry { get; set; } = null!;
}
