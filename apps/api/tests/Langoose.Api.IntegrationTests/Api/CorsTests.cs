using Langoose.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Langoose.Api.IntegrationTests.Api;

public sealed class CorsTests
{
    [Fact]
    public async Task Preflight_request_allows_a_configured_origin()
    {
        await using var host = await CorsTestHost.CreateAsync("http://localhost:5173");
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await host.Client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal("http://localhost:5173", Assert.Single(origins));
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials));
        Assert.Equal("true", Assert.Single(credentials));
    }

    [Fact]
    public async Task Preflight_request_does_not_allow_an_unconfigured_origin()
    {
        await using var host = await CorsTestHost.CreateAsync("http://localhost:5173");
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await host.Client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private sealed class CorsTestHost(IHost host) : IAsyncDisposable
    {
        public HttpClient Client { get; } = host.GetTestClient();

        public static async Task<CorsTestHost> CreateAsync(params string[] allowedOrigins)
        {
            var builder = new HostBuilder();

            builder.ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    var options = new CorsSettings
                    {
                        AllowedOrigins = allowedOrigins
                    };

                    services.AddRouting();
                    services.AddCors(cors =>
                    {
                        cors.AddPolicy(CorsSettings.SectionName, policy =>
                        {
                            if (options.AllowedOrigins.Length == 0)
                            {
                                return;
                            }

                            policy.WithOrigins(options.AllowedOrigins)
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials();
                        });
                    });
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseCors(CorsSettings.SectionName);
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/health", () => Results.Ok("ok"));
                    });
                });
            });

            var startedHost = await builder.StartAsync();

            return new CorsTestHost(startedHost);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await host.StopAsync();
            host.Dispose();
        }
    }
}
