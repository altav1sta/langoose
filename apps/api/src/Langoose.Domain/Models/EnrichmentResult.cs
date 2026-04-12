namespace Langoose.Domain.Models;

public sealed record EnrichmentResult(
    EnrichedEntry[] SourceEntries,
    EnrichedEntry[] TargetEntries,
    EnrichedContext[] Contexts);
