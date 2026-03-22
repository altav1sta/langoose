namespace Langoose.Api.Models;

public sealed record ImportCsvRequest(string FileName, string CsvContent);
