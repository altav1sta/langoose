namespace Langoose.Api.Models;

public sealed record EnrichmentResponse(
    string EnglishText,
    List<string> RussianGlosses,
    string Difficulty,
    string PartOfSpeech,
    List<ExampleCandidate> Examples,
    List<string> ValidationWarnings,
    List<string> AcceptedVariants);
