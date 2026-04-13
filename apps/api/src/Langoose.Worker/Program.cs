using Langoose.Core.Configuration;
using Langoose.Core.Providers;
using Langoose.Core.Services;
using Langoose.Data;
using Langoose.Domain.Services;
using Langoose.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? throw new InvalidOperationException("Connection string 'AppDatabase' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IEnrichmentProvider, LocalEnrichmentProvider>();
builder.Services.AddScoped<IEnrichmentProcessor, EnrichmentProcessor>();

builder.Services.Configure<EnrichmentSettings>(
    builder.Configuration.GetSection(EnrichmentSettings.SectionName));

builder.Services.AddFeatureManagement();

builder.Services.AddHostedService<EnrichmentBackgroundService>();

var host = builder.Build();
host.Run();
