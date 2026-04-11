namespace Langoose.Domain.Models;

public sealed record ExampleCandidate(
    string SentenceText,
    string ClozeText,
    string TranslationHint,
    double QualityScore,
    string Origin);
