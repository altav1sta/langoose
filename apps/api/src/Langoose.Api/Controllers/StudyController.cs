using Langoose.Api.Models;
using Langoose.Auth.Data.Models;
using Langoose.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Authorize]
[Route("study")]
public sealed class StudyController(
    IStudyService studyService,
    UserManager<AuthUser> userManager) : ControllerBase
{
    [HttpGet("next")]
    public async Task<ActionResult<StudyCardResponse>> Next(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var card = await studyService.GetNextCardAsync(user.Id, cancellationToken);

        if (card is null)
        {
            return NotFound();
        }

        return Ok(new StudyCardResponse(
            card.DictionaryEntryId,
            card.EntryContextId,
            card.Prompt,
            card.SentenceTranslation,
            card.Translations,
            card.GrammarHint,
            card.Difficulty));
    }

    [HttpPost("answer")]
    public async Task<ActionResult<StudyAnswerResult>> Answer(
        [FromBody] StudyAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var result = await studyService.SubmitAnswerAsync(
            user.Id, request.EntryId, request.EntryContextId, request.SubmittedAnswer, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(new StudyAnswerResult(
            result.Verdict,
            result.NormalizedAnswer,
            result.ExpectedAnswer,
            result.FeedbackCode,
            result.NextDueAtUtc));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProgressDashboardResponse>> Dashboard(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var dashboard = await studyService.GetDashboardAsync(user.Id, cancellationToken);

        return Ok(new ProgressDashboardResponse(
            dashboard.TotalEntries,
            dashboard.DueNow,
            dashboard.NewEntries,
            dashboard.StudiedToday));
    }
}
