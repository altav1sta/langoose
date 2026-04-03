namespace Langoose.Domain.Models;

public sealed class ContentFlag
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public required Guid ItemId { get; set; }
    public required string Reason { get; set; }
    public string? Details { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
