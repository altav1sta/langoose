namespace Langoose.Api.Models;

public sealed record UserEntryRequest(
    string UserInputTerm,
    string SourceLanguage = "ru",
    string TargetLanguage = "en",
    string? Notes = null,
    List<string>? Tags = null,
    string? Type = null);
