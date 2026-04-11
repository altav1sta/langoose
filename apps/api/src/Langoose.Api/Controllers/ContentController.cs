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

        var input = new ReportIssueInput(request.DictionaryEntryId, request.Reason);
        await contentService.ReportIssueAsync(user.Id, input, cancellationToken);

        return Accepted();
    }
}
