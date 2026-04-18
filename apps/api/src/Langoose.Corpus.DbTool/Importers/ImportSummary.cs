namespace Langoose.Corpus.DbTool.Importers;

/// <summary>
/// Result of running an <see cref="ICorpusImporter"/>. Reported back to the
/// DbTool host so it can log per-source counts after each import run.
/// </summary>
public sealed record ImportSummary(
    string Source,
    string SourceVersion,
    long EntriesImported);
