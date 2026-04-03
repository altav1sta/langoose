using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Langoose.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveConfigurationBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Langoose")
            ?? throw new InvalidOperationException("Connection string 'Langoose' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveConfigurationBasePath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            currentDirectory,
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "Langoose.Api")),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "src", "Langoose.Api")),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "Langoose.Api")),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "src", "Langoose.Api")),
            Path.GetFullPath(Path.Combine(currentDirectory, "src", "Langoose.Api")),
            Path.GetFullPath(Path.Combine(currentDirectory, "apps", "api", "Langoose.Api")),
            Path.GetFullPath(Path.Combine(currentDirectory, "apps", "api", "src", "Langoose.Api"))
        };

        foreach (var candidate in candidates.Distinct())
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }
        }

        return currentDirectory;
    }
}
