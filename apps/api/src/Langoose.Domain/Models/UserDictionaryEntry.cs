using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed class UserDictionaryEntry
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public Guid? SourceEntryId { get; set; }
    public Guid? TargetEntryId { get; set; }
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }
    public required string UserInputTerm { get; set; }
    public string? UserInputTranslation { get; set; }
    public required string PartOfSpeech { get; set; }
    public required EnrichmentStatus EnrichmentStatus { get; set; }
    public int EnrichmentAttempts { get; set; }
    public DateTimeOffset? EnrichmentNotBefore { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = [];
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DictionaryEntry? SourceEntry { get; set; }
    public DictionaryEntry? TargetEntry { get; set; }
}
