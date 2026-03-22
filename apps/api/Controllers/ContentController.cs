using Langoose.Api.Models;
using Langoose.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("content")]
public sealed class ContentController(
    AuthService authService,
    ContentService contentService) : ControllerBase
{
    [HttpPost("enrich")]
    public async Task<ActionResult<EnrichmentResponse>> Enrich(
        [FromBody] EnrichmentRequest request,
        CancellationToken cancellationToken)
    {
        var user = await authService.GetUserFromTokenAsync(
            Request.Headers.Authorization,
            cancellationToken);

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
        var user = await authService.GetUserFromTokenAsync(
            Request.Headers.Authorization,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        await contentService.ReportIssueAsync(user.Id, request, cancellationToken);

        return Accepted();
    }
}
