namespace Langoose.Domain.Models;

public sealed record ImportPayloadEntry(
    string Language,
    string Text,
    string Pos);
