using Langoose.Api.Models;
using Langoose.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("study")]
public sealed class StudyController(
    AuthService authService,
    StudyService studyService) : ControllerBase
{
    [HttpGet("next")]
    public async Task<ActionResult<StudyCardResponse>> Next(CancellationToken cancellationToken)
    {
        var user = await authService.GetUserFromTokenAsync(
            Request.Headers.Authorization,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var card = await studyService.GetNextCardAsync(user.Id, cancellationToken);

        return card is null ? NotFound() : Ok(card);
    }

    [HttpPost("answer")]
    public async Task<ActionResult<StudyAnswerResult>> Answer(
        [FromBody] StudyAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var user = await authService.GetUserFromTokenAsync(
            Request.Headers.Authorization,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var result = await studyService.SubmitAnswerAsync(user.Id, request, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProgressDashboardResponse>> Dashboard(CancellationToken cancellationToken)
    {
        var user = await authService.GetUserFromTokenAsync(
            Request.Headers.Authorization,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await studyService.GetDashboardAsync(user.Id, cancellationToken));
    }
}
