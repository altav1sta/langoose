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
    [HttpGet("entries")]
    public async Task<ActionResult<IReadOnlyList<DictionaryListItem>>> GetEntries(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await dictionaryService.GetVisibleEntriesAsync(user.Id, cancellationToken));
    }

    [HttpPost("entries")]
    public async Task<ActionResult<UserDictionaryEntry>> AddEntry(
        [FromBody] UserEntryRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var input = new AddUserEntryInput(
            request.UserInputTerm,
            request.UserInputTranslation,
            request.SourceLanguage,
            request.TargetLanguage,
            request.Notes,
            request.Tags,
            request.Type);

        return Ok(await dictionaryService.AddUserEntryAsync(user.Id, input, cancellationToken));
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

            return Ok(new ImportCsvResponse(result.RowCount, result.PendingCount, result.Errors));
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

        await dictionaryService.ClearUserDataAsync(user.Id, cancellationToken);

        return NoContent();
    }
}
