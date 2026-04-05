using Langoose.Api.Models;
using Langoose.Api.Services;
using Xunit;

namespace Langoose.Api.UnitTests.Services;

public sealed class EnrichmentServiceTests
{
    [Fact]
    public void Enrich_WhenGlossLooksEnglish_ReturnsValidationWarnings()
    {
        var enrichmentService = new EnrichmentService();

        var response = enrichmentService.Enrich(
            new EnrichmentRequest("mysterious word", ["english gloss"], "phrase"));

        Assert.NotEmpty(response.ValidationWarnings);
    }
}
