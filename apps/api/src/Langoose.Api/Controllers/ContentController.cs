using Langoose.Api.Models;
using Langoose.Api.Services;
using Langoose.Auth.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("content")]
public sealed class ContentController(
    ContentService contentService,
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

        return Ok(contentService.Enrich(request));
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

        await contentService.ReportIssueAsync(user.Id, request, cancellationToken);

        return Accepted();
    }
}
