namespace Langoose.Domain.Models;

public sealed record EnrichedContext(
    string SourceText,
    string SourceCloze,
    string TargetText,
    string SourceFormText,
    string TargetFormText,
    string? Difficulty);
