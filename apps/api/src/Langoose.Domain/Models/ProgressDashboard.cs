namespace Langoose.Domain.Models;

public sealed record ProgressDashboard(
    int TotalItems,
    int DueNow,
    int NewItems,
    int BaseItems,
    int CustomItems,
    int StudiedToday);
