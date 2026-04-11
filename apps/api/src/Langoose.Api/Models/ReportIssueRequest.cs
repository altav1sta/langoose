namespace Langoose.Api.Models;

public sealed record ReportIssueRequest(Guid DictionaryEntryId, string Reason);
