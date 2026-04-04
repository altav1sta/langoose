using Langoose.Api.Models;
using Langoose.Api.Services;
using Langoose.Auth.Data.Models;
using Langoose.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Authorize]
[Route("dictionary")]
public sealed class DictionaryController(
    DictionaryService dictionaryService,
    UserManager<AuthUser> userManager) : ControllerBase
{
    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<DictionaryItem>>> GetItems(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await dictionaryService.GetItemsAsync(user.Id, cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<DictionaryItem>> AddItem(
        [FromBody] DictionaryItemRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await dictionaryService.AddItemAsync(user.Id, request, cancellationToken));
    }

    [HttpPatch("items/{itemId:guid}")]
    public async Task<ActionResult<DictionaryItem>> PatchItem(
        Guid itemId,
        [FromBody] DictionaryItemPatchRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var updated = await dictionaryService.PatchItemAsync(user.Id, itemId, request, cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportCsvResponse>> Import(
        [FromBody] ImportCsvRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await dictionaryService.ImportCsvAsync(user.Id, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid CSV format",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet("export")]
    public async Task<ActionResult<string>> Export(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        return Content(
            await dictionaryService.ExportCsvAsync(user.Id, cancellationToken),
            "text/csv");
    }

    [HttpDelete("custom-data")]
    public async Task<IActionResult> ClearCustomData(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        await dictionaryService.ClearCustomDataAsync(user.Id, cancellationToken);

        return NoContent();
    }
}
