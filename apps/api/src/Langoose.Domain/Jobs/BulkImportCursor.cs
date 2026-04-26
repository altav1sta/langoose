namespace Langoose.Domain.Jobs;

public sealed record BulkImportCursor(
    int LastRank,
    string LastWord,
    string LastPos);
