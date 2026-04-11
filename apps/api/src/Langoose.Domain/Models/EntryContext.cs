namespace Langoose.Domain.Models;

public sealed class EntryContext
{
    public required Guid Id { get; init; }
    public required Guid DictionaryEntryId { get; set; }
    public required string Text { get; set; }
    public required string Cloze { get; set; }
    public string? Difficulty { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DictionaryEntry DictionaryEntry { get; set; } = null!;
    public ICollection<ContextTranslation> SourceContextTranslations { get; set; } = [];
    public ICollection<ContextTranslation> TargetContextTranslations { get; set; } = [];
}
