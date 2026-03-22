using Langoose.Api.Infrastructure;
using Langoose.Api.Models;

namespace Langoose.Api.Services;

public sealed class AuthService(IDataStore dataStore)
{
    public async Task<AuthResponse> EmailSignInAsync(
        EmailSignInRequest request,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = store.Users.FirstOrDefault(candidate => candidate.Email == email);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? email.Split('@')[0]
                    : request.Name.Trim()
            };
            store.Users.Add(user);
        }

        var session = new SessionToken { UserId = user.Id };
        store.SessionTokens.Add(session);
        await dataStore.SaveAsync(store, cancellationToken);

        return new AuthResponse(user.Id, user.Email, user.Name, session.Token);
    }

    public async Task<AuthResponse> SocialSignInAsync(
        SocialSignInRequest request,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = store.Users.FirstOrDefault(candidate =>
            candidate.Email == email ||
            (candidate.Provider == request.Provider &&
             candidate.ProviderUserId == request.ProviderUserId));

        if (user is null)
        {
            user = new User
            {
                Email = email,
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? request.ProviderUserId
                    : request.Name.Trim(),
                Provider = request.Provider.Trim(),
                ProviderUserId = request.ProviderUserId.Trim()
            };
            store.Users.Add(user);
        }
        else
        {
            user.Provider = request.Provider.Trim();
            user.ProviderUserId = request.ProviderUserId.Trim();
        }

        var session = new SessionToken { UserId = user.Id };
        store.SessionTokens.Add(session);
        await dataStore.SaveAsync(store, cancellationToken);

        return new AuthResponse(user.Id, user.Email, user.Name, session.Token);
    }

    public async Task<User?> GetUserFromTokenAsync(
        string? authHeader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var store = await dataStore.LoadAsync(cancellationToken);
        var session = store.SessionTokens.FirstOrDefault(candidate => candidate.Token == token);

        return session is null
            ? null
            : store.Users.FirstOrDefault(candidate => candidate.Id == session.UserId);
    }
}
