using System.Text.Json.Serialization;

namespace Langoose.Domain.Models;

public sealed class ExampleSentence
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public string SentenceText { get; set; } = string.Empty;
    public string ClozeText { get; set; } = string.Empty;
    public string TranslationHint { get; set; } = string.Empty;
    public double QualityScore { get; set; } = 0.7;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentOrigin Origin { get; set; } = ContentOrigin.Dataset;
}
