using Langoose.Core.Services;
using Langoose.Domain.Models;
using Xunit;

namespace Langoose.Core.UnitTests.Services;

public sealed class EnrichmentServiceTests
{
    [Fact]
    public void Enrich_WhenGlossLooksEnglish_ReturnsValidationWarnings()
    {
        var enrichmentService = new EnrichmentService();

        var response = enrichmentService.Enrich(
            new EnrichmentInput("mysterious word", ["english gloss"], "phrase"));

        Assert.NotEmpty(response.ValidationWarnings);
    }
}
