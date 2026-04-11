namespace Langoose.Domain.Models;

public sealed record StudyCard(
    Guid DictionaryEntryId,
    string Prompt,
    string TranslationHint,
    string? Difficulty);
