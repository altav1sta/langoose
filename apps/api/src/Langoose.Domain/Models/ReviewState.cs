namespace Langoose.Domain.Models;

public sealed class ReviewState
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public required Guid ItemId { get; set; }
    public required double Stability { get; set; }
    public required DateTimeOffset DueAtUtc { get; set; }
    public int LapseCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
}
