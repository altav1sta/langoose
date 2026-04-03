using Langoose.Api.Models;
using Langoose.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("email-sign-in")]
    public Task<AuthResponse> EmailSignIn(
        [FromBody] EmailSignInRequest request,
        CancellationToken cancellationToken) =>
        authService.EmailSignInAsync(request, cancellationToken);

    [HttpPost("social-sign-in")]
    public Task<AuthResponse> SocialSignIn(
        [FromBody] SocialSignInRequest request,
        CancellationToken cancellationToken) =>
        authService.SocialSignInAsync(request, cancellationToken);

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await authService.GetUserFromTokenAsync(
            Request.Headers.Authorization,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new MeResponse(user.Id, user.Email ?? "", user.DisplayName));
    }
}
