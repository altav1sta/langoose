namespace Langoose.Domain.Models;

public sealed record EnrichmentInput(string EnglishText, List<string>? RussianGlosses, string? ItemKind);
