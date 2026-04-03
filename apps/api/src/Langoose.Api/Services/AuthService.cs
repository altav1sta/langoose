using Langoose.Api.Models;
using Langoose.Auth.Data;
using Langoose.Auth.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Api.Services;

public sealed class AuthService(AuthDbContext authDbContext, UserManager<AuthUser> userManager)
{
    private static string? NormalizeDisplayName(string? rawName)
    {
        var trimmedName = rawName?.Trim();

        return string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
    }

    public async Task<AuthResponse> EmailSignInAsync(
        EmailSignInRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null)
        {
            user = new AuthUser
            {
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                DisplayName = NormalizeDisplayName(request.Name),
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException("Failed to create auth user for placeholder sign-in flow.");
            }
        }

        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        authDbContext.AuthSessions.Add(session);
        await authDbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(user.Id, user.Email ?? email, user.DisplayName, session.Token);
    }

    public async Task<AuthResponse> SocialSignInAsync(
        SocialSignInRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var provider = request.Provider.Trim();
        var providerUserId = request.ProviderUserId.Trim();
        var user = await userManager.Users.FirstOrDefaultAsync(x =>
            x.Email == email ||
            (x.Provider == provider &&
             x.ProviderUserId == providerUserId), cancellationToken);

        if (user is null)
        {
            user = new AuthUser
            {
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                DisplayName = NormalizeDisplayName(request.Name),
                Provider = provider,
                ProviderUserId = providerUserId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException("Failed to create auth user for placeholder social sign-in flow.");
            }
        }
        else
        {
            user.Provider = provider;
            user.ProviderUserId = providerUserId;
            user.DisplayName = NormalizeDisplayName(request.Name);

            var updateResult = await userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException("Failed to update auth user for placeholder social sign-in flow.");
            }
        }

        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        authDbContext.AuthSessions.Add(session);
        await authDbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(user.Id, user.Email ?? email, user.DisplayName, session.Token);
    }

    public async Task<AuthUser?> GetUserFromTokenAsync(
        string? authHeader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var session = await authDbContext.AuthSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        return session is null
            ? null
            : await userManager.Users.FirstOrDefaultAsync(x => x.Id == session.UserId, cancellationToken);
    }
}
