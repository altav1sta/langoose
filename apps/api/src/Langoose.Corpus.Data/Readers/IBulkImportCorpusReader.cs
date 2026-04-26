namespace Langoose.Corpus.Data.Readers;

public interface IBulkImportCorpusReader
{
    Task<bool> SnapshotsExistAsync(
        string language,
        string wiktionarySource,
        string wordfreqSource,
        CancellationToken ct);

    IAsyncEnumerable<WiktionaryBundle> StreamBundlesAsync(
        BulkImportCorpusQuery query,
        CancellationToken ct);
}
