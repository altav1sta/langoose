namespace Langoose.Domain.Models;

public sealed record ProgressDashboard(
    int TotalEntries,
    int DueNow,
    int NewEntries,
    int StudiedToday);
