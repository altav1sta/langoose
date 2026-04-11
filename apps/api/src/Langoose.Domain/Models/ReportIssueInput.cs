namespace Langoose.Domain.Models;

public sealed record ReportIssueInput(Guid DictionaryEntryId, string Reason);
