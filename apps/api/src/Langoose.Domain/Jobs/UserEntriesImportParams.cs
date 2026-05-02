namespace Langoose.Domain.Jobs;

public sealed record UserEntriesImportParams(
    int BatchSize,
    int MaxRetries);
