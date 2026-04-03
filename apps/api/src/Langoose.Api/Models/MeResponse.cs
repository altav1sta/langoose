namespace Langoose.Api.Models;

public sealed record MeResponse(Guid UserId, string Email, string? Name);
