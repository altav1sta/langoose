namespace Langoose.Domain.Models;

public sealed record EnrichedEntry(
    string Text,
    string? BaseFormText,
    string? PartOfSpeech,
    string? GrammarLabel,
    string? Difficulty);
