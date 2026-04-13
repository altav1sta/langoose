namespace Langoose.Api.Models;

public sealed record UserEntryRequest(
    string UserInputTerm,
    string? UserInputTranslation = null,
    string SourceLanguage = "ru",
    string TargetLanguage = "en",
    string PartOfSpeech = "noun",
    string? Notes = null,
    List<string>? Tags = null);
