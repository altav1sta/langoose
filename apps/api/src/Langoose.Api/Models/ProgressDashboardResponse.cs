namespace Langoose.Api.Models;

public sealed record ProgressDashboardResponse(
    int TotalEntries,
    int DueNow,
    int NewEntries,
    int StudiedToday);
