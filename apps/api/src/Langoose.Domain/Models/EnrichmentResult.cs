using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed record EnrichmentResult(
    Guid UserEntryId,
    EnrichmentStatus Status,
    EnrichedEntry[]? SourceEntries,
    EnrichedEntry[]? TargetEntries);
