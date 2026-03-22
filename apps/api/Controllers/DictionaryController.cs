using Langoose.Api.Models;
using Langoose.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Langoose.Api.Controllers;

[ApiController]
[Route("dictionary")]
public sealed class DictionaryController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly DictionaryService _dictionaryService;

    public DictionaryController(AuthService authService, DictionaryService dictionaryService)
    {
        _authService = authService;
        _dictionaryService = dictionaryService;
    }

    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<DictionaryItem>>> GetItems(CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await _dictionaryService.GetItemsAsync(user.Id, cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<DictionaryItem>> AddItem([FromBody] DictionaryItemRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await _dictionaryService.AddItemAsync(user.Id, request, cancellationToken));
    }

    [HttpPatch("items/{itemId:guid}")]
    public async Task<ActionResult<DictionaryItem>> PatchItem(Guid itemId, [FromBody] DictionaryItemPatchRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var updated = await _dictionaryService.PatchItemAsync(user.Id, itemId, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportCsvResponse>> Import([FromBody] ImportCsvRequest request, CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _dictionaryService.ImportCsvAsync(user.Id, request, cancellationToken));
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
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Content(await _dictionaryService.ExportCsvAsync(user.Id, cancellationToken), "text/csv");
    }

    [HttpDelete("custom-data")]
    public async Task<IActionResult> ClearCustomData(CancellationToken cancellationToken)
    {
        var user = await _authService.GetUserFromTokenAsync(Request.Headers.Authorization, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        await _dictionaryService.ClearCustomDataAsync(user.Id, cancellationToken);
        return NoContent();
    }
}
