namespace Langoose.Domain.Models;

public sealed class EntryTranslation
{
    public required Guid SourceEntryId { get; init; }
    public required Guid TargetEntryId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DictionaryEntry SourceEntry { get; set; } = null!;
    public DictionaryEntry TargetEntry { get; set; } = null!;
}
