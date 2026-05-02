namespace Langoose.Domain.Jobs;

public sealed record BulkJobState
{
    public int TotalCount { get; init; }
    public int ProcessedCount { get; init; }
    public int FailedCount { get; init; }
    public string? Cursor { get; init; }
    public string? ErrorMessage { get; init; }
}
