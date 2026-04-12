namespace Langoose.Api.Models;

public sealed record StudyAnswerRequest(Guid EntryId, Guid? EntryContextId, string SubmittedAnswer);
