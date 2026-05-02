using System.Text.Json;
using Langoose.Data;
using Langoose.Data.Json;
using Langoose.Domain.Enums;
using Langoose.Domain.Jobs;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Langoose.DbTool;

internal static class JobCommands
{
    public static async Task<int> ListJobsAsync(string[] args)
    {
        var typeFilter = GetOption(args, "--type") is { } typeText
            ? Enum.Parse<JobType>(typeText, ignoreCase: true)
            : (JobType?)null;
        var statusFilter = GetOption(args, "--status") is { } statusText
            ? Enum.Parse<JobStatus>(statusText, ignoreCase: true)
            : (JobStatus?)null;
        var limit = GetIntOption(args, "--limit") ?? 20;

        using var host = Program.BuildHost(args, configureAppDatabase: true, configureAuthDatabase: false);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();

        var query = db.BackgroundJobs.AsNoTracking().AsQueryable();

        if (typeFilter is { } t)
            query = query.Where(x => x.Type == t);
        if (statusFilter is { } s)
            query = query.Where(x => x.Status == s);

        var jobs = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();

        if (jobs.Count == 0)
        {
            Console.WriteLine("No jobs found.");
            return 0;
        }

        Console.WriteLine($"{"Id",-36}  {"Type",-13}  {"Status",-10}  {"Created",-25}  Settings");
        foreach (var job in jobs)
        {
            Console.WriteLine(
                $"{job.Id,-36}  {job.Type,-13}  {job.Status,-10}  {job.CreatedAtUtc:yyyy-MM-dd HH:mm:ss zzz}  {Truncate(job.Settings, 80)}");
        }

        return 0;
    }

    public static async Task<int> ShowJobAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: show-job <id>");
            return 1;
        }

        var id = Guid.Parse(args[0]);

        using var host = Program.BuildHost(args, configureAppDatabase: true, configureAuthDatabase: false);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();

        var job = await db.BackgroundJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

        if (job is null)
        {
            Console.Error.WriteLine($"Job {id} not found.");
            return 1;
        }

        Console.WriteLine($"Id:              {job.Id}");
        Console.WriteLine($"Type:            {job.Type}");
        Console.WriteLine($"Status:          {job.Status}");
        Console.WriteLine($"Created:         {job.CreatedAtUtc:O}");
        Console.WriteLine($"Started:         {job.StartedAtUtc?.ToString("O") ?? "-"}");
        Console.WriteLine($"Finished:        {job.FinishedAtUtc?.ToString("O") ?? "-"}");
        Console.WriteLine($"Updated:         {job.UpdatedAtUtc:O}");
        Console.WriteLine();
        Console.WriteLine("Settings:");
        Console.WriteLine(PrettyPrintJson(job.Settings));
        Console.WriteLine();
        Console.WriteLine("ExecutionState:");
        Console.WriteLine(job.ExecutionState is null ? "  (none)" : PrettyPrintJson(job.ExecutionState));

        return 0;
    }

    public static async Task<int> CancelJobAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cancel-job <id>");
            return 1;
        }

        var id = Guid.Parse(args[0]);

        using var host = Program.BuildHost(args, configureAppDatabase: true, configureAuthDatabase: false);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();

        var job = await db.BackgroundJobs.FirstOrDefaultAsync(x => x.Id == id);

        if (job is null)
        {
            Console.Error.WriteLine($"Job {id} not found.");
            return 1;
        }

        if (job.Status is not (JobStatus.Pending or JobStatus.Running))
        {
            Console.Error.WriteLine($"Job {id} is in terminal status {job.Status} and cannot be cancelled.");
            return 1;
        }

        job.Status = JobStatus.Cancelled;
        job.FinishedAtUtc ??= DateTimeOffset.UtcNow;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        Console.WriteLine($"Cancelled job {id}.");

        return 0;
    }

    public static async Task<int> ResubmitJobAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: resubmit-job <id>");
            return 1;
        }

        var id = Guid.Parse(args[0]);

        using var host = Program.BuildHost(args, configureAppDatabase: true, configureAuthDatabase: false);
        var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();

        var source = await db.BackgroundJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

        if (source is null)
        {
            Console.Error.WriteLine($"Job {id} not found.");
            return 1;
        }

        if (source.Status is not (JobStatus.Failed or JobStatus.Cancelled))
        {
            Console.Error.WriteLine(
                $"Job {id} is {source.Status}; only Failed and Cancelled jobs can be resubmitted.");
            return 1;
        }

        var resumedState = BuildResumedExecutionState(source.Type, source.ExecutionState);
        var now = DateTimeOffset.UtcNow;
        var newJob = new BackgroundJob
        {
            Id = Guid.CreateVersion7(),
            Type = source.Type,
            Status = JobStatus.Pending,
            Settings = source.Settings,
            ExecutionState = resumedState,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.BackgroundJobs.Add(newJob);
        await db.SaveChangesAsync();

        Console.WriteLine(
            $"Resubmitted as {newJob.Id}{(resumedState is null ? "" : " (resumes from saved cursor)")}.");

        return 0;
    }

    private static string? BuildResumedExecutionState(JobType type, string? sourceExecutionState)
    {
        if (sourceExecutionState is null)
            return null;

        switch (type)
        {
            case JobType.CorpusImport:
                var prior = JsonSerializer.Deserialize<BulkJobState>(
                    sourceExecutionState, AppJsonOptions.Default);
                if (prior?.Cursor is null)
                    return null;

                // Resume from the saved cursor with zeroed counters — the
                // resubmitted run accumulates fresh totals from this point.
                var resumed = new BulkJobState { Cursor = prior.Cursor };
                return JsonSerializer.Serialize(resumed, AppJsonOptions.Default);
            default:
                return null;
        }
    }

    private static string PrettyPrintJson(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");

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
