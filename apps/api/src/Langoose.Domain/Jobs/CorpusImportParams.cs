namespace Langoose.Domain.Jobs;

public sealed record CorpusImportParams(
    string Language,
    string Source,
    string? StartCursor);
