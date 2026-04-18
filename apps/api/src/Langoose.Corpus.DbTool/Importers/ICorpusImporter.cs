namespace Langoose.Corpus.DbTool.Importers;

/// <summary>
/// Contract for a single-source corpus importer. Each implementation owns
/// its source-specific tables and import logic, but reports progress through
/// this common interface so the DbTool host can wire them uniformly.
/// </summary>
public interface ICorpusImporter
{
    /// <summary>
    /// Stable name of the source. Used as the value of the
    /// <c>corpus_metadata</c> key, e.g. <c>wiktionary_en</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Reads the source file from <paramref name="sourcePath"/> and bulk-loads
    /// it into the corpus database. Idempotent: replaces any existing rows
    /// for this source within a single transaction.
    /// </summary>
    Task<ImportSummary> ImportAsync(string sourcePath, CancellationToken ct = default);
}
