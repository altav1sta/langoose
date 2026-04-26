using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed class BackgroundJob
{
    public required Guid Id { get; init; }
    public required JobType Type { get; init; }
    public required JobStatus Status { get; set; }
    public required string Settings { get; init; }
    public string? ExecutionState { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}
