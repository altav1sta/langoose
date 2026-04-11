namespace Langoose.Api.Models;

public sealed record ImportCsvResponse(int RowCount, int PendingCount, List<string> Errors);
