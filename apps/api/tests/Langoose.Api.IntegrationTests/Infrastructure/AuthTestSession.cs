using System.Net.Http.Json;
using System.Text.Json;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal sealed class AuthTestSession(HttpClient client)
{
    private readonly Dictionary<string, string> cookies = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GetAntiforgeryAsync()
    {
        var response = await SendAsync(CreateRequest(HttpMethod.Get, "/auth/antiforgery", requestToken: null));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryTokenPayload>(JsonOptions);

        return payload?.RequestToken ?? throw new InvalidOperationException("Antiforgery response did not include a request token.");
    }

    public Task<HttpResponseMessage> GetAsync(string path) =>
        SendAsync(CreateRequest(HttpMethod.Get, path, requestToken: null));

    public Task<HttpResponseMessage> PostAsync(string path, string? requestToken = null) =>
        SendAsync(CreateRequest(HttpMethod.Post, path, requestToken));

    public Task<HttpResponseMessage> PostJsonAsync<T>(string path, T payload, string? requestToken = null)
    {
        var request = CreateRequest(HttpMethod.Post, path, requestToken);
        request.Content = JsonContent.Create(payload);

        return SendAsync(request);
    }

    public async Task<T?> ReadAsJsonAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions);

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? requestToken)
    {
        var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(requestToken))
        {
            request.Headers.Add("X-CSRF-TOKEN", requestToken);
        }

        ApplyCookies(request);

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var response = await client.SendAsync(request);
        StoreCookies(response);

        return response;
    }

    private void ApplyCookies(HttpRequestMessage request)
    {
        if (cookies.Count == 0)
        {
            return;
        }

        request.Headers.Add("Cookie", string.Join("; ", cookies.Select(x => $"{x.Key}={x.Value}")));
    }

    private void StoreCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var header in values)
        {
            var pair = header.Split(';', 2, StringSplitOptions.TrimEntries)[0];
            var separatorIndex = pair.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = pair[..separatorIndex];
            var value = pair[(separatorIndex + 1)..];
            cookies[name] = value;
        }
    }
}
