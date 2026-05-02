namespace Langoose.Worker.Configuration;

public sealed class UserEntriesImportSettings
{
    public const string SectionName = "UserEntriesImport";

    public int PollIntervalSeconds { get; init; }
    public int BatchSize { get; init; }
    public int MaxRetries { get; init; }
}
