using Langoose.Api.Models;
using Langoose.Api.Tests.Infrastructure;
using Xunit;

namespace Langoose.Api.Tests.Services;

public sealed class EnrichmentServiceTests
{
    [Fact]
    public async Task Enrich_WhenGlossLooksEnglish_ReturnsValidationWarnings()
    {
        await using var context = await TestAppContext.CreateAsync();

        var response = context.EnrichmentService.Enrich(
            new EnrichmentRequest("mysterious word", ["english gloss"], "phrase"));

        Assert.NotEmpty(response.ValidationWarnings);
    }
}
