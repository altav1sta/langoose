namespace Langoose.Domain.Jobs;

public sealed record BulkImportState(
    string? Cursor,
    int ProcessedCount,
    int HeuristicAcceptedCount,
    int HeuristicRejectedCount,
    string? ErrorMessage);
