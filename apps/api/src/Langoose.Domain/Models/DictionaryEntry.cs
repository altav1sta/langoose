namespace Langoose.Domain.Models;

public sealed class DictionaryEntry
{
    public required Guid Id { get; init; }
    public required string Language { get; set; }
    public required string Text { get; set; }
    public required bool IsBaseForm { get; set; }
    public Guid? BaseEntryId { get; set; }
    public string? GrammarLabel { get; set; }
    public string? Difficulty { get; set; }
    public required bool IsPublic { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DictionaryEntry? BaseEntry { get; set; }
    public ICollection<DictionaryEntry> DerivedForms { get; set; } = [];
    public ICollection<EntryContext> Contexts { get; set; } = [];
    public ICollection<EntryTranslation> SourceTranslations { get; set; } = [];
    public ICollection<EntryTranslation> TargetTranslations { get; set; } = [];
}
