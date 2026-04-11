namespace Langoose.Domain.Models;

public sealed record StudyCard(
    Guid ItemId,
    string Prompt,
    string TranslationHint,
    List<string> Glosses,
    string ItemKind,
    string SourceType,
    string Difficulty);
