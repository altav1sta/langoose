namespace Langoose.Auth.Data.Models;

public sealed class AuthSession
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string Token { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public AuthUser? User { get; set; }
}
