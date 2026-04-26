using Langoose.Core.BulkImport;
using Langoose.Core.Configuration;
using Langoose.Core.Providers;
using Langoose.Core.Services;
using Langoose.Corpus.Data.Readers;
using Langoose.Data;
using Langoose.Domain.Imports;
using Langoose.Domain.Services;
using Langoose.Worker.Configuration;
using Langoose.Worker.Jobs;
using Langoose.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? throw new InvalidOperationException("Connection string 'AppDatabase' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddDbContextFactory<AppDbContext>(
    options => options.UseNpgsql(connectionString),
    lifetime: ServiceLifetime.Singleton);

var corpusConnectionString = builder.Configuration.GetConnectionString("CorpusDatabase")
    ?? throw new InvalidOperationException("Connection string 'CorpusDatabase' is not configured.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(corpusConnectionString));
builder.Services.AddSingleton<IImportSourceReader, WiktionaryImportSourceReader>();

builder.Services.AddScoped<IEnrichmentProvider, LocalEnrichmentProvider>();
builder.Services.AddScoped<IEnrichmentProcessor, EnrichmentProcessor>();

builder.Services.Configure<EnrichmentSettings>(
    builder.Configuration.GetSection(EnrichmentSettings.SectionName));
builder.Services.Configure<BulkImportSettings>(
    builder.Configuration.GetSection(BulkImportSettings.SectionName));

builder.Services.AddSingleton(sp =>
{
    var bulkImportOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BulkImportSettings>>();

    return new HeuristicFilter(bulkImportOptions.Value.Heuristic);
});
builder.Services.AddScoped<BulkImportJobHandler>();

builder.Services.AddFeatureManagement();

builder.Services.AddHostedService<EnrichmentBackgroundService>();
builder.Services.AddHostedService<BulkImportBackgroundService>();

var host = builder.Build();
host.Run();
