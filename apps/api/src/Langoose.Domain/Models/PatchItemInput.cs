namespace Langoose.Domain.Models;

public sealed record PatchItemInput(
    List<string>? RussianGlosses,
    string? PartOfSpeech,
    string? Difficulty,
    string? Notes,
    List<string>? Tags,
    string? Status);
