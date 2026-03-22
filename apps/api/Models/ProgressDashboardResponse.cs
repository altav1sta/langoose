namespace Langoose.Api.Models;

public sealed record ProgressDashboardResponse(
    int TotalItems,
    int DueNow,
    int NewItems,
    int BaseItems,
    int CustomItems,
    int StudiedToday);
