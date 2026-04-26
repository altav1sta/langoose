namespace Langoose.Domain.Jobs;

public sealed record BulkImportState(
    BulkImportCursor? Cursor,
    int ProcessedCount,
    int HeuristicAcceptedCount,
    int HeuristicRejectedCount,
    string? ErrorMessage);
