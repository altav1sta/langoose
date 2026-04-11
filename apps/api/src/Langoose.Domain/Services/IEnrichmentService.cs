using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IEnrichmentService
{
    EnrichmentResult Enrich(EnrichmentInput input);
}
