using Microsoft.AspNetCore.Identity;

namespace Langoose.Auth.Data.Models;

public sealed class AuthUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public string? Provider { get; set; }
    public string? ProviderUserId { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
