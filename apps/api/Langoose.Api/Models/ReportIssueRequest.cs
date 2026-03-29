namespace Langoose.Api.Models;

public sealed record ReportIssueRequest(Guid ItemId, string Reason, string? Details);
