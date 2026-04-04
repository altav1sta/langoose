namespace Langoose.Api.Models;

public sealed record CurrentUserResponse(Guid UserId, string Email);
