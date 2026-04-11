namespace Langoose.Domain.Models;

public sealed class UserProgress
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public required Guid DictionaryEntryId { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public required DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset? LastReviewedAtUtc { get; set; }
    public double? Stability { get; set; }
    public double? Difficulty { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DictionaryEntry DictionaryEntry { get; set; } = null!;
}
