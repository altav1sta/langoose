namespace Langoose.Domain.Models;

public sealed class Sense
{
    public required Guid Id { get; init; }
    public required Guid DictionaryEntryId { get; set; }
    public required int SenseIndex { get; set; }
    public string? Gloss { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }

    public DictionaryEntry DictionaryEntry { get; set; } = null!;
    public ICollection<SenseTranslation> Translations { get; set; } = [];
}
