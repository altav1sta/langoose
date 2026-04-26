namespace Langoose.Domain.Models;

public sealed record ImportPayloadSense(
    int SenseIndex,
    string? Gloss,
    ImportPayloadTranslation[] Translations);
