using Langoose.Api.Models;
using Langoose.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("content")]
public sealed class ContentController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ContentService _contentService;

    public ContentController(AuthService authService, ContentService contentService)
    {
        _authService = authService;
        _contentService = contentService;
    }

    [HttpPost("enrich")]
    public async Task<ActionResult<EnrichmentResponse>> Enrich([FromBody] EnrichmentRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(_contentService.Enrich(request));
    }

    [HttpPost("report-issue")]
    public async Task<IActionResult> ReportIssue([FromBody] ReportIssueRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        await _contentService.ReportIssueAsync(user.Id, request, cancellationToken);
        return Accepted();
    }
}