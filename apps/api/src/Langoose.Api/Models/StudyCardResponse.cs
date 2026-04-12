namespace Langoose.Api.Models;

public sealed record StudyCardResponse(
    Guid DictionaryEntryId,
    Guid? EntryContextId,
    string Prompt,
    string SentenceTranslation,
    IReadOnlyList<string> Translations,
    string? GrammarHint,
    string? Difficulty);
