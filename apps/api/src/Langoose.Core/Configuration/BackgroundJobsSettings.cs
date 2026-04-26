namespace Langoose.Core.Configuration;

public sealed class BackgroundJobsSettings
{
    public const string SectionName = "BackgroundJobs";

    public int PollIntervalSeconds { get; init; } = 5;
}
