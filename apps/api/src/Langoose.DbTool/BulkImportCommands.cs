using System.Text.Json;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Jobs;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Langoose.DbTool;

internal static class BulkImportCommands
{
    public static async Task<int> SubmitBulkImportAsync(string[] args)
    {
        var language = GetRequiredOption(args, "--lang");
        var source = GetRequiredOption(args, "--source");

        var settings = new BulkImportParams(language, source);

        using var host = Program.BuildHost(args, configureAppDatabase: true, configureAuthDatabase: false);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();

        var now = DateTimeOffset.UtcNow;
        var job = new BackgroundJob
        {
            Id = Guid.CreateVersion7(),
            Type = JobType.BulkImport,
            Status = JobStatus.Pending,
            Settings = JsonSerializer.Serialize(settings, AppJsonOptions.Default),
            ExecutionState = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.BackgroundJobs.Add(job);
        await db.SaveChangesAsync();

        Console.WriteLine(
            $"Submitted bulk-import job {job.Id} (lang={language}, source={source}).");

        return 0;
    }

    private static string GetRequiredOption(string[] args, string name) =>
        GetOption(args, name)
            ?? throw new ArgumentException($"Missing required option: {name}");

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        return null;
    }
}
