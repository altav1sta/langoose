using System.Text.Json.Serialization;

namespace Langoose.Domain.Models;

public sealed class DictionaryItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? OwnerId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SourceType SourceType { get; set; }

    public string EnglishText { get; set; } = string.Empty;
    public List<string> RussianGlosses { get; set; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemKind ItemKind { get; set; }

    public string PartOfSpeech { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "A1";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DictionaryItemStatus Status { get; set; } = DictionaryItemStatus.Active;

    public string CreatedByFlow { get; set; } = "seed";
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<string> Distractors { get; set; } = [];
    public List<string> AcceptedVariants { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
