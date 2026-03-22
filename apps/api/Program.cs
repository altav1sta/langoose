using Langoose.Api.Infrastructure;
using Langoose.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IDataStore, FileDataStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<EnrichmentService>();
builder.Services.AddSingleton<DictionaryService>();
builder.Services.AddSingleton<StudyService>();
builder.Services.AddSingleton<ContentService>();
builder.Services.AddSingleton<DataSeeder>();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseExceptionHandler();
await app.Services.GetRequiredService<DataSeeder>().SeedAsync();

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