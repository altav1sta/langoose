namespace Langoose.Api.Models;

public sealed record StudyCardResponse(
    Guid DictionaryEntryId,
    string Prompt,
    string TranslationHint,
    string? Difficulty);
