namespace Langoose.Api.Models;

public sealed record ImportCsvResponse(int TotalRows, int ImportedRows, int SkippedRows, List<string> Errors);
