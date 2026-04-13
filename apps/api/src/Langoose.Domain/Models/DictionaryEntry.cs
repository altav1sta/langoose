namespace Langoose.Domain.Models;

public sealed class DictionaryEntry
{
    public required Guid Id { get; init; }
    public required string Language { get; set; }
    public required string Text { get; set; }
    public Guid? BaseEntryId { get; set; }
    public required string PartOfSpeech { get; set; }
    public string? GrammarLabel { get; set; }
    public string? Difficulty { get; set; }
    public required bool IsPublic { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DictionaryEntry? BaseEntry { get; set; }
    public ICollection<DictionaryEntry> DerivedForms { get; set; } = [];
    public ICollection<EntryContext> Contexts { get; set; } = [];
    public ICollection<DictionaryEntry> Translations { get; set; } = [];
}
