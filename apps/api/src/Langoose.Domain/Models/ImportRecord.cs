namespace Langoose.Domain.Models;

public sealed class ImportRecord
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; set; }
    public required string FileName { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedRows { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
