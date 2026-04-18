namespace Langoose.Corpus.Data.Models;

/// <summary>
/// A specific meaning of a Kaikki entry. Glosses are the human-readable
/// definitions; translations are sense-scoped (one sense can map to
/// different translations across languages).
/// </summary>
public sealed record WiktionarySense(
    string[]? Glosses = null,
    WiktionaryTranslation[]? Translations = null);
