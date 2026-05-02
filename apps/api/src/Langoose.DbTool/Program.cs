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
    private static readonly string[] KnownCommands =
    [
        "apply-app-migrations",
        "apply-auth-migrations",
        "seed-app",
        "submit-corpus-import",
        "list-jobs",
        "show-job",
        "cancel-job",
        "resubmit-job"
    ];

    public static async Task<int> Main(string[] args)
    {
        // No args: design-time tools (e.g. `dotnet ef`) invoke the
        // assembly to build a host for migrations. Build and exit silently.
        if (args.Length == 0)
        {
            using var host = BuildHost(args);
            return 0;
        }

        var command = args[0];
        var commandArgs = args[1..];

        return command switch
        {
            "apply-app-migrations" => await ApplyAppMigrationsAsync(commandArgs),
            "apply-auth-migrations" => await ApplyAuthMigrationsAsync(commandArgs),
            "seed-app" => await SeedAppAsync(commandArgs),
            "submit-corpus-import" => await CorpusImportCommands.SubmitCorpusImportAsync(commandArgs),
            "list-jobs" => await JobCommands.ListJobsAsync(commandArgs),
            "show-job" => await JobCommands.ShowJobAsync(commandArgs),
            "cancel-job" => await JobCommands.CancelJobAsync(commandArgs),
            "resubmit-job" => await JobCommands.ResubmitJobAsync(commandArgs),
            _ => UnknownCommand(command)
        };
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: '{command}'.");
        Console.Error.WriteLine($"Available commands: {string.Join(", ", KnownCommands)}.");

        return 1;
    }

    public static HostApplicationBuilder CreateApplicationBuilder(string[] args)
    {
        return Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
    }

    public static IHost BuildHost(
        string[] args,
        bool configureAppDatabase = true,
        bool configureAuthDatabase = true,
        bool configureAppSeeding = false)
    {
        var builder = CreateApplicationBuilder(args);

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

    private static async Task<int> ApplyAppMigrationsAsync(string[] args)
    {
        using var host = BuildHost(
            args,
            configureAppDatabase: true,
            configureAuthDatabase: false);

        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        await context.Database.MigrateAsync();

        return 0;
    }

    private static async Task<int> ApplyAuthMigrationsAsync(string[] args)
    {
        using var host = BuildHost(
            args,
            configureAppDatabase: false,
            configureAuthDatabase: true);

        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AuthDbContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        await context.Database.MigrateAsync();

        return 0;
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
