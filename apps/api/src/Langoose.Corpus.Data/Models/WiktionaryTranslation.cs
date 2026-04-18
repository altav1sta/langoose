namespace Langoose.Corpus.Data.Models;

/// <summary>
/// A translation of a sense to another language. <see cref="Code"/> is the
/// ISO 639 language code; <see cref="Word"/> is the translated text. Tags
/// carry grammatical metadata such as gender.
/// </summary>
public sealed record WiktionaryTranslation(
    string? Code = null,
    string? Lang = null,
    string? Word = null,
    string[]? Tags = null);
