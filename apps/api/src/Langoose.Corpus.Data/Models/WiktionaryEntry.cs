namespace Langoose.Corpus.Data.Models;

/// <summary>
/// The Kaikki entry document, preserved as-is from JSONL. Property names map
/// to the source via <see cref="System.Text.Json.JsonNamingPolicy.SnakeCaseLower"/>
/// configured on the serializer (no per-field attributes). Forms and senses
/// are nullable because not every Kaikki entry has them.
/// </summary>
public sealed record WiktionaryEntry(
    string Word,
    string LangCode,
    string Pos,
    WiktionaryForm[]? Forms = null,
    WiktionarySense[]? Senses = null);
