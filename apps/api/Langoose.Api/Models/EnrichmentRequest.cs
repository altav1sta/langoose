namespace Langoose.Api.Models;

public sealed record EnrichmentRequest(string EnglishText, List<string>? RussianGlosses, string? ItemKind);
