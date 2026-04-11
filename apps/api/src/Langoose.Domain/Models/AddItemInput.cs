namespace Langoose.Domain.Models;

public sealed record AddItemInput(
    string EnglishText,
    List<string>? RussianGlosses,
    string? ItemKind,
    string? PartOfSpeech,
    string? Difficulty,
    string? Notes,
    List<string>? Tags,
    string? CreatedByFlow,
    bool GenerateExamples = true);
