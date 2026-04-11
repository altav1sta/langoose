using Langoose.Api.Controllers;
using Langoose.Api.Middleware;
using Langoose.Auth.Data;
using Langoose.Auth.Data.Models;
using Langoose.Core.Services;
using Langoose.Data;
using Langoose.Data.Seeding;
using Langoose.Domain.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal sealed class ApiTestHost(IHost host) : IAsyncDisposable
{
    internal static readonly Guid AuthenticatedUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal const string AuthenticatedEmail = "learner@example.com";

    public HttpClient Client { get; } = host.GetTestClient();

    public IServiceProvider Services => host.Services;

    public static async Task<ApiTestHost> CreateAsync(bool authenticated = false)
    {
        var appDbName = $"langoose-app-tests-{Guid.NewGuid():N}";
        var authDbName = $"langoose-auth-tests-{Guid.NewGuid():N}";
        var builder = new HostBuilder();

        builder.ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services =>
            {
                services.AddProblemDetails();
                services.AddHttpContextAccessor();
                services.AddAuthorization();
                services.AddAntiforgery(options =>
                {
                    options.Cookie.Name = "langoose.csrf";
                    options.Cookie.HttpOnly = false;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.Cookie.Path = "/";
                    options.Cookie.IsEssential = true;
                    options.HeaderName = "X-CSRF-TOKEN";
                });
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(appDbName));
                services.AddDbContext<AuthDbContext>(options => options.UseInMemoryDatabase(authDbName));
                services.AddIdentityCore<AuthUser>(options =>
                    {
                        options.User.RequireUniqueEmail = true;
                        options.Password.RequiredLength = 8;
                        options.Password.RequireDigit = false;
                        options.Password.RequireLowercase = false;
                        options.Password.RequireUppercase = false;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Lockout.AllowedForNewUsers = true;
                        options.Lockout.MaxFailedAccessAttempts = 5;
                        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    })
                    .AddEntityFrameworkStores<AuthDbContext>();
                services.AddScoped<IDictionaryService, DictionaryService>();
                services.AddScoped<IStudyService, StudyService>();
                services.AddScoped<IContentService, ContentService>();
                services.AddScoped<DatabaseSeeder>();
                services.AddControllers()
                    .AddApplicationPart(typeof(DictionaryController).Assembly);
            });
            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseMiddleware<AntiforgeryValidationMiddleware>();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        });

        var startedHost = await builder.StartAsync();
        var host = new ApiTestHost(startedHost);
        await host.SeedAsync();

        if (authenticated)
        {
            host.Client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, AuthenticatedUserId.ToString());
            host.Client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, AuthenticatedEmail);
        }

        return host;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await host.StopAsync();
        host.Dispose();
    }

    private async Task SeedAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AuthUser>>();
        if (await userManager.FindByEmailAsync(AuthenticatedEmail) is not null)
        {
            return;
        }

        var user = new AuthUser
        {
            Id = AuthenticatedUserId,
            Email = AuthenticatedEmail,
            UserName = AuthenticatedEmail,
            LockoutEnabled = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var result = await userManager.CreateAsync(user, "password123");

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to seed authenticated test user.");
        }
    }
}
