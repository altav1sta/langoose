namespace Langoose.Corpus.Data.Readers;

public sealed record BulkImportCorpusQuery(
    string Language,
    string WiktionarySource,
    string WordfreqSource,
    int? TopFrequencyRank,
    int? Cursor_LastRank,
    string? Cursor_LastWord,
    string? Cursor_LastPos);
