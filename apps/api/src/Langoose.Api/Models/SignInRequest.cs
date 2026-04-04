using System.ComponentModel.DataAnnotations;

namespace Langoose.Api.Models;

public sealed record SignInRequest(
    [param: Required, EmailAddress] string Email,
    [param: Required, MinLength(8)] string Password);
