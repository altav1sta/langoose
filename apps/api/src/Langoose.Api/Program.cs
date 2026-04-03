using System.Text.Json;
using System.Text.Json.Serialization;
using Langoose.Data;
using Langoose.Auth.Data;
using Langoose.Auth.Data.Models;
using Langoose.Data.Seeding;
using Langoose.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var appConnectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? throw new InvalidOperationException("Connection string 'AppDatabase' is not configured.");
var authConnectionString = builder.Configuration.GetConnectionString("AuthDatabase")
    ?? throw new InvalidOperationException("Connection string 'AuthDatabase' is not configured.");

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

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
    })
    .AddEntityFrameworkStores<AuthDbContext>();

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

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EnrichmentService>();
builder.Services.AddScoped<DictionaryService>();
builder.Services.AddScoped<StudyService>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<DatabaseSeeder>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    });
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseExceptionHandler();

await using (var scope = app.Services.CreateAsyncScope())
{
    var authDbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await authDbContext.Database.MigrateAsync();

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

app.UseCors();
app.MapHealthChecks("/health");
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = "Langoose API",
    status = "ok",
    endpoints = new[]
    {
        "GET /health",
        "POST /auth/email-sign-in",
        "POST /auth/social-sign-in",
        "GET /connect/authorize",
        "POST /connect/token",
        "GET /study/next",
        "POST /study/answer",
        "GET /study/dashboard",
        "GET/POST/PATCH /dictionary/items",
        "POST /dictionary/import",
        "GET /dictionary/export",
        "POST /content/enrich",
        "POST /content/report-issue"
    }
}));

app.Run();
