namespace Langoose.Domain.Models;

public sealed record AddUserEntryInput(
    string UserInputTerm,
    string? UserInputTranslation,
    string SourceLanguage,
    string TargetLanguage,
    string? Notes = null,
    List<string>? Tags = null,
    string? Type = null);
