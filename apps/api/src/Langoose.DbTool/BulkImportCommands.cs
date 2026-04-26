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
        var wiktionarySource = GetRequiredOption(args, "--wiktionary-source");
        var wordfreqSource = GetRequiredOption(args, "--wordfreq-source");
        var topRank = GetIntOption(args, "--top-rank");
        var limit = GetIntOption(args, "--limit");

        var settings = new BulkImportParams(
            language,
            wiktionarySource,
            wordfreqSource,
            topRank,
            limit,
            RequestedByUserId: null);

        using var host = Program.BuildHost(args, configureAppDatabase: true, configureAuthDatabase: false);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();

        var now = DateTimeOffset.UtcNow;
        var job = new BackgroundJob
        {
            Id = Guid.CreateVersion7(),
            Type = JobType.BulkImport,
            Status = JobStatus.Pending,
            Settings = JsonSerializer.Serialize(settings, BackgroundJobJsonContext.Default.BulkImportParams),
            ExecutionState = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.BackgroundJobs.Add(job);
        await db.SaveChangesAsync();

        Console.WriteLine($"Submitted bulk-import job {job.Id} (lang={language}, wiktionary={wiktionarySource}, wordfreq={wordfreqSource}).");

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

    private static int? GetIntOption(string[] args, string name) =>
        GetOption(args, name) is { } value ? int.Parse(value) : null;
}
