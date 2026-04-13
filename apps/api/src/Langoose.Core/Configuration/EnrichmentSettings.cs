namespace Langoose.Core.Configuration;

public sealed class EnrichmentSettings
{
    public const string SectionName = "Enrichment";

    public int PollIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 10;
    public int MaxRetries { get; init; } = 3;
}
