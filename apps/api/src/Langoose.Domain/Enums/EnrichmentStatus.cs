namespace Langoose.Domain.Enums;

public enum EnrichmentStatus
{
    Pending,
    Enriched,
    InvalidSource,
    InvalidTarget,
    InvalidLink,
    ProviderError
}
