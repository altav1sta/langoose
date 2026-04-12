namespace Langoose.Domain.Models;

public sealed record StudyCard(
    Guid DictionaryEntryId,
    Guid? EntryContextId,
    string Prompt,
    string SentenceTranslation,
    IReadOnlyList<string> Translations,
    string? GrammarHint,
    string? Difficulty);
