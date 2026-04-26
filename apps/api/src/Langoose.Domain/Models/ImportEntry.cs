using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed class ImportEntry
{
    public required Guid Id { get; init; }
    public required EntrySource Source { get; set; }
    public required string SourceRefId { get; set; }
    public required string Language { get; set; }
    public required string Text { get; set; }
    public required string PartOfSpeech { get; set; }
    public required string Payload { get; set; }
    public required ImportEntryStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public float? AiConfidence { get; set; }
    public string? AiReasoning { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public Guid? PromotedEntryId { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}
