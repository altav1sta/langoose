using System.Net;
using Langoose.Api.IntegrationTests.Infrastructure;
using Langoose.Api.Models;
using Xunit;

namespace Langoose.Api.IntegrationTests.Api;

public sealed class AuthFlowTests
{
    [Fact]
    public async Task Sign_up_sign_out_sign_in_and_me_follow_the_m1_contract()
    {
        await using var host = await AuthApiTestHost.CreateAsync();
        var session = host.CreateSession();
        var email = $"learner+{Guid.NewGuid():N}@example.com";
        const string password = "password123";

        var signUpToken = await session.GetAntiforgeryAsync();
        var signUpResponse = await session.PostJsonAsync("/auth/sign-up", new SignUpRequest(email, password), signUpToken);
        var signUpPayload = await session.ReadAsJsonAsync<CurrentUserResponse>(signUpResponse);

        Assert.Equal(HttpStatusCode.OK, signUpResponse.StatusCode);
        Assert.NotNull(signUpPayload);
        Assert.Equal(email, signUpPayload.Email);

        var meAfterSignUpResponse = await session.GetAsync("/auth/me");
        var meAfterSignUpPayload = await session.ReadAsJsonAsync<CurrentUserResponse>(meAfterSignUpResponse);

        Assert.Equal(HttpStatusCode.OK, meAfterSignUpResponse.StatusCode);
        Assert.NotNull(meAfterSignUpPayload);
        Assert.Equal(email, meAfterSignUpPayload.Email);
        Assert.Equal(signUpPayload.UserId, meAfterSignUpPayload.UserId);

        var signOutToken = await session.GetAntiforgeryAsync();
        var signOutResponse = await session.PostAsync("/auth/sign-out", signOutToken);
        var meAfterSignOutResponse = await session.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterSignOutResponse.StatusCode);

        var signInToken = await session.GetAntiforgeryAsync();
        var signInResponse = await session.PostJsonAsync("/auth/sign-in", new SignInRequest(email, password), signInToken);
        var signInPayload = await session.ReadAsJsonAsync<CurrentUserResponse>(signInResponse);
        var meAfterSignInResponse = await session.GetAsync("/auth/me");
        var meAfterSignInPayload = await session.ReadAsJsonAsync<CurrentUserResponse>(meAfterSignInResponse);

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        Assert.NotNull(signInPayload);
        Assert.Equal(email, signInPayload.Email);
        Assert.Equal(HttpStatusCode.OK, meAfterSignInResponse.StatusCode);
        Assert.NotNull(meAfterSignInPayload);
        Assert.Equal(signInPayload.UserId, meAfterSignInPayload.UserId);
    }

    [Fact]
    public async Task Duplicate_sign_up_email_returns_conflict()
    {
        await using var host = await AuthApiTestHost.CreateAsync();
        var email = $"existing+{Guid.NewGuid():N}@example.com";
        await host.CreateUserAsync(email, "password123");
        var session = host.CreateSession();
        var signUpToken = await session.GetAntiforgeryAsync();

        var response = await session.PostJsonAsync("/auth/sign-up", new SignUpRequest(email, "password123"), signUpToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_password_returns_unauthorized()
    {
        await using var host = await AuthApiTestHost.CreateAsync();
        var existingEmail = $"existing+{Guid.NewGuid():N}@example.com";
        await host.CreateUserAsync(existingEmail, "password123");
        var session = host.CreateSession();
        var signInToken = await session.GetAntiforgeryAsync();

        var response = await session.PostJsonAsync(
            "/auth/sign-in",
            new SignInRequest(existingEmail, "wrongpass1"),
            signInToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_returns_unauthorized()
    {
        await using var host = await AuthApiTestHost.CreateAsync();
        var session = host.CreateSession();
        var signInToken = await session.GetAntiforgeryAsync();

        var response = await session.PostJsonAsync(
            "/auth/sign-in",
            new SignInRequest("missing@example.com", "password123"),
            signInToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_invalid_passwords_trigger_lockout()
    {
        await using var host = await AuthApiTestHost.CreateAsync();
        var email = $"locked+{Guid.NewGuid():N}@example.com";
        await host.CreateUserAsync(email, "password123");
        var session = host.CreateSession();
        var signInToken = await session.GetAntiforgeryAsync();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var response = await session.PostJsonAsync("/auth/sign-in", new SignInRequest(email, "wrongpass1"), signInToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var lockedResponse = await session.PostJsonAsync("/auth/sign-in", new SignInRequest(email, "wrongpass1"), signInToken);

        Assert.Equal((HttpStatusCode)423, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task Missing_antiforgery_token_rejects_unsafe_auth_requests()
    {
        await using var host = await AuthApiTestHost.CreateAsync();
        var session = host.CreateSession();
        var email = $"csrf+{Guid.NewGuid():N}@example.com";
        const string password = "password123";

        var missingCsrfSignUp = await session.PostJsonAsync("/auth/sign-up", new SignUpRequest(email, password));

        Assert.Equal(HttpStatusCode.BadRequest, missingCsrfSignUp.StatusCode);

        var signUpToken = await session.GetAntiforgeryAsync();
        var signUpResponse = await session.PostJsonAsync("/auth/sign-up", new SignUpRequest(email, password), signUpToken);

        Assert.Equal(HttpStatusCode.OK, signUpResponse.StatusCode);

        var missingCsrfSignOut = await session.PostAsync("/auth/sign-out");

        Assert.Equal(HttpStatusCode.BadRequest, missingCsrfSignOut.StatusCode);
    }
}
