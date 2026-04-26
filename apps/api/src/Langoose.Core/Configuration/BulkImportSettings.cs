namespace Langoose.Core.Configuration;

public sealed class BulkImportSettings
{
    public const string SectionName = "BulkImport";

    public int BatchSize { get; init; } = 100;
    public HeuristicFilterSettings Heuristic { get; init; } = new();
}
