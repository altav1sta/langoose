using Langoose.Api.Models;
using Langoose.Auth.Data.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IAntiforgery antiforgery,
    SignInManager<AuthUser> signInManager,
    UserManager<AuthUser> userManager) : ControllerBase
{
    [HttpGet("antiforgery")]
    public ActionResult<AntiforgeryTokenResponse> Antiforgery()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        if (string.IsNullOrWhiteSpace(tokens.RequestToken))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok(new AntiforgeryTokenResponse(tokens.RequestToken));
    }

    [HttpPost("sign-up")]
    public async Task<ActionResult<CurrentUserResponse>> SignUp(
        [FromBody] SignUpRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = NormalizeEmail(request.Email);
        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return Conflict();
        }

        var user = new AuthUser
        {
            Email = email,
            UserName = email,
            LockoutEnabled = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            return ValidationProblem(ToModelState(createResult.Errors.ToArray()));
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        return Ok(ToCurrentUserResponse(user));
    }

    [HttpPost("sign-in")]
    public async Task<ActionResult<CurrentUserResponse>> SignIn(
        [FromBody] SignInRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = NormalizeEmail(request.Email);
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return Unauthorized();
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked);
        }

        if (!signInResult.Succeeded)
        {
            return Unauthorized();
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        return Ok(ToCurrentUserResponse(user));
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOutUser()
    {
        await signInManager.SignOutAsync();

        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(user.Id, user.Email ?? ""));
    }

    private static ModelStateDictionary ToModelState(IReadOnlyList<IdentityError>? errors)
    {
        var modelState = new ModelStateDictionary();

        if (errors is null)
        {
            return modelState;
        }

        foreach (var error in errors)
        {
            modelState.AddModelError(ToModelStateKey(error.Code), error.Description);
        }

        return modelState;
    }

    private static string ToModelStateKey(string errorCode) =>
        errorCode.StartsWith("Password", StringComparison.Ordinal)
            ? nameof(SignUpRequest.Password)
            : errorCode.Contains("Email", StringComparison.Ordinal) ||
              errorCode.Contains("UserName", StringComparison.Ordinal)
                ? nameof(SignUpRequest.Email)
                : "";

    private static string NormalizeEmail(string rawEmail) =>
        rawEmail.Trim().ToLowerInvariant();

    private static CurrentUserResponse ToCurrentUserResponse(AuthUser user) =>
        new(user.Id, user.Email ?? "");
}
