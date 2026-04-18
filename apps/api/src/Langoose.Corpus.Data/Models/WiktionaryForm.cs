namespace Langoose.Corpus.Data.Models;

/// <summary>
/// An inflected form within a Kaikki entry's <c>forms</c> array. Tags
/// describe the morphological role (e.g. <c>plural</c>, <c>past</c>).
/// </summary>
public sealed record WiktionaryForm(
    string Form,
    string[]? Tags = null);
