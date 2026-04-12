namespace Langoose.Domain.Models;

public sealed record EnrichmentRequest(
    string RawText,
    string? RawTranslation,
    string SourceLanguage,
    string TargetLanguage);
