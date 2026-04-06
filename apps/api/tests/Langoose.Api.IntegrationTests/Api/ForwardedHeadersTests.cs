using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Langoose.Api.IntegrationTests.Api;

public sealed class ForwardedHeadersTests
{
    [Fact]
    public async Task Antiforgery_endpoint_accepts_a_forwarded_https_request_when_no_proxy_restrictions_are_configured()
    {
        await using var host = await ForwardedHeadersTestHost.CreateAsync(restrictToKnownProxy: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/antiforgery");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Antiforgery_endpoint_fails_for_a_forwarded_https_request_when_unknown_proxies_are_not_trusted()
    {
        await using var host = await ForwardedHeadersTestHost.CreateAsync(restrictToKnownProxy: true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/antiforgery");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private sealed class ForwardedHeadersTestHost(IHost host) : IAsyncDisposable
    {
        public HttpClient Client { get; } = host.GetTestClient();

        public static async Task<ForwardedHeadersTestHost> CreateAsync(bool restrictToKnownProxy)
        {
            var builder = new HostBuilder();

            builder.ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.UseEnvironment("Staging");
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAntiforgery(options =>
                    {
                        options.Cookie.Name = "langoose.csrf";
                        options.Cookie.HttpOnly = false;
                        options.Cookie.SameSite = SameSiteMode.Lax;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        options.Cookie.Path = "/";
                        options.Cookie.IsEssential = true;
                        options.HeaderName = "X-CSRF-TOKEN";
                    });
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
                        options.KnownProxies.Clear();
                        options.KnownIPNetworks.Clear();

                        if (restrictToKnownProxy)
                        {
                            options.KnownProxies.Add(IPAddress.Parse("127.0.0.2"));
                        }
                    });
                });
                webHost.Configure(app =>
                {
                    app.UseExceptionHandler(errorApp =>
                    {
                        errorApp.Run(context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            return Task.CompletedTask;
                        });
                    });
                    app.Use((context, next) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
                        return next();
                    });
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/auth/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
                        {
                            var tokens = antiforgery.GetAndStoreTokens(context);
                            return Results.Ok(tokens.RequestToken);
                        });
                    });
                });
            });

            var startedHost = await builder.StartAsync();

            return new ForwardedHeadersTestHost(startedHost);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await host.StopAsync();
            host.Dispose();
        }
    }
}
