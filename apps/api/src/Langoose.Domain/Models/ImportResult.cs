namespace Langoose.Domain.Models;

public sealed record ImportResult(int RowCount, int PendingCount, List<string> Errors);
