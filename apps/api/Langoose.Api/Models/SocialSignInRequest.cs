namespace Langoose.Api.Models;

public sealed record SocialSignInRequest(string Provider, string ProviderUserId, string Email, string? Name);
