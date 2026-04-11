namespace Langoose.Domain.Models;

public sealed class ImportRecord
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public string? FileHash { get; set; }
    public required int RowCount { get; set; }
    public int PendingCount { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
