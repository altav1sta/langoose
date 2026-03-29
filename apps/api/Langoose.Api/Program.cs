using Langoose.Data;
using Langoose.Data.Seeding;
using Langoose.Domain.Abstractions;
using Langoose.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Langoose")
    ?? throw new InvalidOperationException("Connection string 'Langoose' is not configured.");

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<IDataStore, PostgresDataStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<EnrichmentService>();
builder.Services.AddSingleton<DictionaryService>();
builder.Services.AddSingleton<StudyService>();
builder.Services.AddSingleton<ContentService>();
builder.Services.AddSingleton<DatabaseSeeder>();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseExceptionHandler();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
}

await app.Services.GetRequiredService<DatabaseSeeder>().SeedAsync();

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
