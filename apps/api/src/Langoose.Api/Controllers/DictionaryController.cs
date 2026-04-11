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
[Route("dictionary")]
public sealed class DictionaryController(
    IDictionaryService dictionaryService,
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

        var input = new AddItemInput(
            request.EnglishText,
            request.RussianGlosses,
            request.ItemKind,
            request.PartOfSpeech,
            request.Difficulty,
            request.Notes,
            request.Tags,
            request.CreatedByFlow,
            request.GenerateExamples);

        return Ok(await dictionaryService.AddItemAsync(user.Id, input, cancellationToken));
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

        var input = new PatchItemInput(
            request.RussianGlosses,
            request.PartOfSpeech,
            request.Difficulty,
            request.Notes,
            request.Tags,
            request.Status);

        var updated = await dictionaryService.PatchItemAsync(user.Id, itemId, input, cancellationToken);

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
            var result = await dictionaryService.ImportCsvAsync(
                user.Id, request.CsvContent, request.FileName, cancellationToken);

            return Ok(new ImportCsvResponse(result.TotalRows, result.ImportedRows, result.SkippedRows, result.Errors));
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
