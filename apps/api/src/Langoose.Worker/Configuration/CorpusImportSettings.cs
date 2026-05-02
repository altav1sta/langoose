namespace Langoose.Worker.Configuration;

public sealed class CorpusImportSettings
{
    public const string SectionName = "CorpusImport";

    public int PollIntervalSeconds { get; init; }
    public int BatchSize { get; init; }
}
