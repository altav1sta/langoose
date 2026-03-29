namespace Langoose.Api.Models;

public sealed record DictionaryItemPatchRequest(
    List<string>? RussianGlosses,
    string? PartOfSpeech,
    string? Difficulty,
    string? Notes,
    List<string>? Tags,
    string? Status);
