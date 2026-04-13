namespace Langoose.Domain.Models;

public sealed record AddUserEntryInput(
    string UserInputTerm,
    string? UserInputTranslation,
    string SourceLanguage,
    string TargetLanguage,
    string PartOfSpeech,
    string? Notes = null,
    List<string>? Tags = null);
