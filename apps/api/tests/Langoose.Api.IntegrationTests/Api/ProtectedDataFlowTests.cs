using System.Net;
using System.Net.Http.Json;
using Langoose.Api.Models;
using Langoose.Api.IntegrationTests.Infrastructure;
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

        var dictionaryResponse = await host.Client.GetAsync("/dictionary/items");
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

        var addItemResponse = await host.Client.PostAsJsonAsync("/dictionary/items", new
        {
            englishText = "improve",
            russianGlosses = new[] { "улучшать" },
            itemKind = "word",
            createdByFlow = "quick-add"
        });
        var patchItemResponse = await host.Client.PatchAsJsonAsync($"/dictionary/items/{Guid.NewGuid()}", new
        {
            notes = "updated"
        });
        var deleteResponse = await host.Client.DeleteAsync("/dictionary/custom-data");
        var answerResponse = await host.Client.PostAsJsonAsync("/study/answer", new
        {
            itemId = Guid.NewGuid(),
            submittedAnswer = "improve"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, addItemResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, patchItemResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, answerResponse.StatusCode);
    }

    [Fact]
    public async Task Fresh_authenticated_user_can_read_dictionary_and_dashboard_without_precreated_app_rows()
    {
        await using var host = await ApiTestHost.CreateAsync(authenticated: true);

        await using var beforeScope = host.Services.CreateAsyncScope();
        var beforeDbContext = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(beforeDbContext.ReviewStates.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));
        Assert.Empty(beforeDbContext.StudyEvents.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));
        Assert.Empty(beforeDbContext.ImportRecords.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));
        Assert.Empty(beforeDbContext.DictionaryItems.Where(x => x.OwnerId == ApiTestHost.AuthenticatedUserId));

        var dictionaryResponse = await host.Client.GetAsync("/dictionary/items");
        var dashboardResponse = await host.Client.GetAsync("/study/dashboard");

        Assert.Equal(HttpStatusCode.OK, dictionaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var items = await dictionaryResponse.Content.ReadFromJsonAsync<IReadOnlyList<Langoose.Domain.Models.DictionaryItem>>();
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<ProgressDashboardResponse>();

        Assert.NotNull(items);
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.True(item.OwnerId is null || item.OwnerId == ApiTestHost.AuthenticatedUserId));

        Assert.NotNull(dashboard);
        Assert.True(dashboard.TotalItems > 0);
        Assert.Equal(0, dashboard.CustomItems);
        Assert.Equal(0, dashboard.StudiedToday);

        await using var afterScope = host.Services.CreateAsyncScope();
        var afterDbContext = afterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(afterDbContext.ReviewStates.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));
        Assert.Empty(afterDbContext.StudyEvents.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));
    }

    [Fact]
    public async Task First_study_request_creates_user_review_state_lazily()
    {
        await using var host = await ApiTestHost.CreateAsync(authenticated: true);

        await using var beforeScope = host.Services.CreateAsyncScope();
        var beforeDbContext = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(beforeDbContext.ReviewStates.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));

        var response = await host.Client.GetAsync("/study/next");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var card = await response.Content.ReadFromJsonAsync<StudyCardResponse>();
        Assert.NotNull(card);

        await using var afterScope = host.Services.CreateAsyncScope();
        var afterDbContext = afterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotEmpty(afterDbContext.ReviewStates.Where(x => x.UserId == ApiTestHost.AuthenticatedUserId));
    }
}
