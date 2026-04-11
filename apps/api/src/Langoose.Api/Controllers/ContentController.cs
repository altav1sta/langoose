using Langoose.Api.Models;
using Langoose.Auth.Data.Models;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Authorize]
[Route("content")]
public sealed class ContentController(
    IContentService contentService,
    UserManager<AuthUser> userManager) : ControllerBase
{
    [HttpPost("enrich")]
    public async Task<ActionResult<EnrichmentResponse>> Enrich(
        [FromBody] EnrichmentRequest request)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var input = new EnrichmentInput(request.EnglishText, request.RussianGlosses, request.ItemKind);
        var result = contentService.Enrich(input);

        return Ok(new EnrichmentResponse(
            result.EnglishText,
            result.RussianGlosses,
            result.Difficulty,
            result.PartOfSpeech,
            [.. result.Examples.Select(e => new Models.ExampleCandidate(
                e.SentenceText, e.ClozeText, e.TranslationHint, e.QualityScore, e.Origin))],
            result.ValidationWarnings,
            result.AcceptedVariants));
    }

    [HttpPost("report-issue")]
    public async Task<IActionResult> ReportIssue(
        [FromBody] ReportIssueRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var input = new ReportIssueInput(request.ItemId, request.Reason, request.Details);
        await contentService.ReportIssueAsync(user.Id, input, cancellationToken);

        return Accepted();
    }
}
