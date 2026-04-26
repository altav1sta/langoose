using Langoose.Core.Configuration;

namespace Langoose.Worker.Configuration;

public sealed class BulkImportSettings
{
    public const string SectionName = "BulkImport";

    public int PollIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 100;
    public HeuristicFilterSettings Heuristic { get; init; } = new();
}
