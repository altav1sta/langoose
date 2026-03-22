namespace Langoose.Api.Models;

public sealed record DictionaryItemRequest(
    string EnglishText,
    List<string>? RussianGlosses,
    string? ItemKind,
    string? PartOfSpeech,
    string? Difficulty,
    string? Notes,
    List<string>? Tags,
    string? CreatedByFlow,
    bool GenerateExamples = true);
