using Langoose.Auth.Data;
using Langoose.Data;
using Langoose.Data.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Langoose.DbTool;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            return 0;
        }

        var command = args[0];
        var commandArgs = args[1..];

        return command switch
        {
            "seed-app" => await SeedAppAsync(commandArgs),
            _ => throw new InvalidOperationException($"Unknown db tool command '{command}'.")
        };
    }

    public static IHost BuildHost(
        string[] args,
        bool configureAppDatabase = true,
        bool configureAuthDatabase = true,
        bool configureAppSeeding = false)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        if (configureAppDatabase)
        {
            var appConnectionString = builder.Configuration.GetConnectionString("AppDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'AppDatabase' is not configured for design-time EF Core operations.");

            builder.Services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseNpgsql(appConnectionString);
            });

            if (configureAppSeeding)
            {
                builder.Services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(appConnectionString);
                });
                builder.Services.AddScoped<DatabaseSeeder>();
            }
        }

        if (configureAuthDatabase)
        {
            var authConnectionString = builder.Configuration.GetConnectionString("AuthDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'AuthDatabase' is not configured for design-time EF Core operations.");

            builder.Services.AddDbContextFactory<AuthDbContext>(options =>
            {
                options.UseNpgsql(authConnectionString);
                options.UseOpenIddict();
            });
        }

        return builder.Build();
    }

    private static async Task<int> SeedAppAsync(string[] args)
    {
        using var host = BuildHost(
            args,
            configureAppDatabase: true,
            configureAuthDatabase: false,
            configureAppSeeding: true);
        await using var scope = host.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        await seeder.SeedAsync();

        return 0;
    }
}
