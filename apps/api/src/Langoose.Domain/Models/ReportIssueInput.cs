namespace Langoose.Domain.Models;

public sealed record ReportIssueInput(Guid ItemId, string Reason, string? Details);
