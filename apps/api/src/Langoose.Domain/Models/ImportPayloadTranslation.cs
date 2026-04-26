namespace Langoose.Domain.Models;

public sealed record ImportPayloadTranslation(
    string Language,
    string Text,
    string? Pos);
