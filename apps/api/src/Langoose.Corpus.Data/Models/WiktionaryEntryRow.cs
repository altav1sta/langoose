namespace Langoose.Corpus.Data.Models;

/// <summary>
/// Maps the columns of <c>wiktionary_entries</c>. The <c>data</c> JSONB
/// column is exposed as a raw JSON string here; deserialise into
/// <see cref="WiktionaryEntry"/> when navigating forms / senses /
/// translations.
/// </summary>
public sealed record WiktionaryEntryRow(
    string LangCode,
    string Word,
    string Pos,
    string SourceVersion,
    string DataJson);
