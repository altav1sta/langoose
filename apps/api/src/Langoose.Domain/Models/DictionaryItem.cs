using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed class DictionaryItem
{
    public required Guid Id { get; init; }
    public Guid? OwnerId { get; set; }
    public required SourceType SourceType { get; set; }
    public required string EnglishText { get; set; }
    public List<string> RussianGlosses { get; set; } = [];
    public required ItemKind ItemKind { get; set; }
    public required string PartOfSpeech { get; set; }
    public required string Difficulty { get; set; }
    public required DictionaryItemStatus Status { get; set; }
    public required string CreatedByFlow { get; set; }
    public required string Notes { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Distractors { get; set; } = [];
    public List<string> AcceptedVariants { get; set; } = [];
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
