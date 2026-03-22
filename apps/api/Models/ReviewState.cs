namespace Langoose.Api.Models;

public sealed class ReviewState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public double Stability { get; set; } = 0.3;
    public DateTimeOffset DueAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int LapseCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
}
