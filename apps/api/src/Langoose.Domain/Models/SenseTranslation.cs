namespace Langoose.Domain.Models;

public sealed class SenseTranslation
{
    public required Guid SourceSenseId { get; set; }
    public required Guid TargetSenseId { get; set; }
    public required int Rank { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public Sense SourceSense { get; set; } = null!;
    public Sense TargetSense { get; set; } = null!;
}
