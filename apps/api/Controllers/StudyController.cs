using Langoose.Api.Models;
using Langoose.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("study")]
public sealed class StudyController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly StudyService _studyService;

    public StudyController(AuthService authService, StudyService studyService)
    {
        _authService = authService;
        _studyService = studyService;
    }

    [HttpGet("next")]
    public async Task<ActionResult<StudyCardResponse>> Next(CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var card = await _studyService.GetNextCardAsync(user.Id, cancellationToken);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpPost("answer")]
    public async Task<ActionResult<StudyAnswerResult>> Answer([FromBody] StudyAnswerRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await _studyService.SubmitAnswerAsync(user.Id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProgressDashboardResponse>> Dashboard(CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await _studyService.GetDashboardAsync(user.Id, cancellationToken));
    }
}