using System.Net;
using System.Net.Http.Json;
using Langoose.Api.IntegrationTests.Infrastructure;
using Langoose.Api.Models;
using Langoose.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Langoose.Api.IntegrationTests.Api;

public sealed class ProtectedDataFlowTests
{
    [Fact]
    public async Task Protected_dictionary_and_study_reads_require_authenticated_access()
    {
        await using var host = await ApiTestHost.CreateAsync();

        var dictionaryResponse = await host.Client.GetAsync("/dictionary/entries");
        var nextCardResponse = await host.Client.GetAsync("/study/next");
        var dashboardResponse = await host.Client.GetAsync("/study/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, dictionaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, nextCardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, dashboardResponse.StatusCode);
    }

    [Fact]
    public async Task Protected_unsafe_requests_return_unauthorized_before_antiforgery_for_anonymous_callers()
    {
        await using var host = await ApiTestHost.CreateAsync();

        var addEntryResponse = await host.Client.PostAsJsonAsync("/dictionary/entries", new
        {
            userInputTerm = "improve",
            sourceLanguage = "ru",
            targetLanguage = "en"
        });
        var deleteResponse = await host.Client.DeleteAsync("/dictionary/custom-data");
        var answerResponse = await host.Client.PostAsJsonAsync("/study/answer", new
        {
            entryId = Guid.NewGuid(),
            submittedAnswer = "improve"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, addEntryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, answerResponse.StatusCode);
    }

    [Fact]
    public async Task Fresh_authenticated_user_can_read_dictionary_and_dashboard()
    {
        await using var host = await ApiTestHost.CreateAsync(authenticated: true);

        await using var beforeScope = host.Services.CreateAsyncScope();
        var beforeDbContext = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(beforeDbContext.UserDictionaryEntries.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));
        Assert.Empty(beforeDbContext.UserProgress.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));

        var dictionaryResponse = await host.Client.GetAsync("/dictionary/entries");
        var dashboardResponse = await host.Client.GetAsync("/study/dashboard");

        Assert.Equal(HttpStatusCode.OK, dictionaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<ProgressDashboardResponse>();

        Assert.NotNull(dashboard);
        Assert.Equal(0, dashboard.StudiedToday);
    }
}
