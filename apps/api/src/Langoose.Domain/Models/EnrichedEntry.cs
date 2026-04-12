namespace Langoose.Domain.Models;

public sealed record EnrichedEntry(
    string Text,
    bool IsBaseForm,
    string? BaseFormText,
    string? GrammarLabel,
    string? Difficulty);
