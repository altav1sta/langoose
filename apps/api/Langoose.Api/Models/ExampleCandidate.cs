namespace Langoose.Api.Models;

public sealed record ExampleCandidate(
    string SentenceText,
    string ClozeText,
    string TranslationHint,
    double QualityScore,
    string Origin);
