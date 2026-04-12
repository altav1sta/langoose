using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Langoose.Api.Configuration;
using Langoose.Api.Middleware;
using Langoose.Auth.Data;
using Langoose.Auth.Data.Models;
using Langoose.Core.Providers;
using Langoose.Core.Services;
using Langoose.Data;
using Langoose.Data.Seeding;
using Langoose.Domain.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IPNetwork = System.Net.IPNetwork;

var builder = WebApplication.CreateBuilder(args);
var appConnectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? throw new InvalidOperationException("Connection string 'AppDatabase' is not configured.");
var authConnectionString = builder.Configuration.GetConnectionString("AuthDatabase")
    ?? throw new InvalidOperationException("Connection string 'AuthDatabase' is not configured.");
var cors = builder.Configuration
    .GetSection(CorsSettings.SectionName)
    .Get<CorsSettings>() ?? new CorsSettings();
var forwardedHeaders = builder.Configuration
    .GetSection(ForwardedHeadersSettings.SectionName)
    .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "langoose.csrf";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(appConnectionString);
});
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseNpgsql(authConnectionString);
    options.UseOpenIddict();
});

builder.Services.AddIdentityCore<AuthUser>(options =>
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

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "langoose.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
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

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<AuthDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();
        options.AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey();
        options.UseAspNetCore();
    });

builder.Services.AddScoped<IDictionaryService, DictionaryService>();
builder.Services.AddScoped<IStudyService, StudyService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IEnrichmentProvider, LocalEnrichmentProvider>();
builder.Services.AddScoped<DatabaseSeeder>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsSettings.SectionName, policy =>
    {
        if (cors.AllowedOrigins.Length == 0)
        {
            return;
        }

        policy.WithOrigins(cors.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

if (forwardedHeaders.Enabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedHost |
                                   ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var proxy in forwardedHeaders.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var ipAddress))
            {
                options.KnownProxies.Add(ipAddress);
            }
        }

        foreach (var network in forwardedHeaders.KnownNetworks)
        {
            if (IPNetwork.TryParse(network, out var ipNetwork))
            {
                options.KnownIPNetworks.Add(ipNetwork);
            }
        }
    });
}

var app = builder.Build();

app.UseExceptionHandler();

if (forwardedHeaders.Enabled)
{
    app.UseForwardedHeaders();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    if (app.Environment.IsDevelopment())
    {
        var authDbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await authDbContext.Database.MigrateAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}

app.UseCors(CorsSettings.SectionName);
app.UseAuthentication();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = "Langoose API",
    status = "ok",
    endpoints = new[]
    {
        "GET /health",
        "GET /auth/antiforgery",
        "POST /auth/sign-up",
        "POST /auth/sign-in",
        "POST /auth/sign-out",
        "GET /auth/me",
        "GET /study/next",
        "POST /study/answer",
        "GET /study/dashboard",
        "GET/POST /dictionary/entries",
        "POST /dictionary/import",
        "GET /dictionary/export",
        "POST /content/report-issue"
    }
}));

app.Run();
