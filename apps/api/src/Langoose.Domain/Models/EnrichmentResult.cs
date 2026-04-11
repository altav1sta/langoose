namespace Langoose.Domain.Models;

public sealed record EnrichmentResult(
    string EnglishText,
    List<string> RussianGlosses,
    string Difficulty,
    string PartOfSpeech,
    List<ExampleCandidate> Examples,
    List<string> ValidationWarnings,
    List<string> AcceptedVariants);
