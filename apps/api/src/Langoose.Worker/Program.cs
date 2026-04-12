using Langoose.Core.Providers;
using Langoose.Data;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? throw new InvalidOperationException("Connection string 'AppDatabase' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IEnrichmentProvider, LocalEnrichmentProvider>();

var host = builder.Build();
host.Run();
