namespace Langoose.Api.Models;

public sealed record StudyCardResponse(
    Guid ItemId,
    string Prompt,
    string TranslationHint,
    List<string> Glosses,
    string ItemKind,
    string SourceType,
    string Difficulty);
