using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed class ExampleSentence
{
    public required Guid Id { get; init; }
    public required Guid ItemId { get; set; }
    public required string SentenceText { get; set; }
    public required string ClozeText { get; set; }
    public required string TranslationHint { get; set; }
    public required double QualityScore { get; set; }
    public required ContentOrigin Origin { get; set; }
}
