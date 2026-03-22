namespace Langoose.Api.Models;

public sealed record AuthResponse(Guid UserId, string Email, string Name, string Token);
