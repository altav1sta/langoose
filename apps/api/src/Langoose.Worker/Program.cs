using Langoose.Core.Configuration;
using Langoose.Core.Heuristic;
using Langoose.Core.Providers;
using Langoose.Core.Services;
using Langoose.Corpus.Data.Readers;
using Langoose.Data;
using Langoose.Domain.Imports;
using Langoose.Domain.Services;
using Langoose.Worker.Configuration;
using Langoose.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? throw new InvalidOperationException("Connection string 'AppDatabase' is not configured.");
var corpusConnectionString = builder.Configuration.GetConnectionString("CorpusDatabase")
    ?? throw new InvalidOperationException("Connection string 'CorpusDatabase' is not configured.");

builder.Services.AddDbContextFactory<AppDbContext>(
    options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddSingleton(NpgsqlDataSource.Create(corpusConnectionString));
builder.Services.AddSingleton<IImportSourceReader, WiktionaryImportSourceReader>();

builder.Services.AddScoped<IEnrichmentProvider, LocalEnrichmentProvider>();
builder.Services.AddScoped<IUserEntriesImportService, UserEntriesImportService>();

builder.Services.Configure<UserEntriesImportSettings>(
    builder.Configuration.GetSection(UserEntriesImportSettings.SectionName));
builder.Services.Configure<CorpusImportSettings>(
    builder.Configuration.GetSection(CorpusImportSettings.SectionName));
builder.Services.Configure<HeuristicFilterSettings>(
    builder.Configuration.GetSection(CorpusImportSettings.SectionName)
        .GetSection(HeuristicFilterSettings.SectionName));

builder.Services.AddSingleton<HeuristicFilter>();
builder.Services.AddScoped<ICorpusImportService, CorpusImportService>();

builder.Services.AddFeatureManagement();

builder.Services.AddHostedService<UserEntriesImportJob>();
builder.Services.AddHostedService<CorpusImportJob>();

var host = builder.Build();

host.Run();
