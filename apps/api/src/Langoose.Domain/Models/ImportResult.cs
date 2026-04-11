namespace Langoose.Domain.Models;

public sealed record ImportResult(int TotalRows, int ImportedRows, int SkippedRows, List<string> Errors);
