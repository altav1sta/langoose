namespace Langoose.Domain.Models;

public sealed record EnrichedEntry(
    string Text,
    bool IsBaseForm,
    string? GrammarLabel,
    string? Difficulty);
