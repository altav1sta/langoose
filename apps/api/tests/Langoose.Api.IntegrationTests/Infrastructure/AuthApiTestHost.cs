using Langoose.Api.Controllers;
using Langoose.Api.Middleware;
using Langoose.Auth.Data;
using Langoose.Auth.Data.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal sealed class AuthApiTestHost(IHost host) : IAsyncDisposable
{
    public IServiceProvider Services => host.Services;

    public static async Task<AuthApiTestHost> CreateAsync()
    {
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
                    .AddSignInManager()
                    .AddEntityFrameworkStores<AuthDbContext>();
                services.AddAuthentication(IdentityConstants.ApplicationScheme)
                    .AddIdentityCookies();
                services.ConfigureApplicationCookie(options =>
                {
                    options.Cookie.Name = "langoose.auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.Cookie.Path = "/";
                    options.Cookie.IsEssential = true;
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToLogin = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }
                    };
                });
                services.AddControllers()
                    .AddApplicationPart(typeof(AuthController).Assembly);
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

        return new AuthApiTestHost(startedHost);
    }

    public AuthTestSession CreateSession() => new(host.GetTestClient());

    public async Task CreateUserAsync(string email, string password)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AuthUser>>();
        var user = new AuthUser
        {
            Email = email,
            UserName = email,
            LockoutEnabled = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed auth test user: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
    }
}
