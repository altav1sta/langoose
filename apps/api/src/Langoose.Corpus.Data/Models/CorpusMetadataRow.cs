namespace Langoose.Corpus.Data.Models;

public sealed record CorpusMetadataRow(
    string Key,
    string Value,
    DateTimeOffset UpdatedAtUtc);
