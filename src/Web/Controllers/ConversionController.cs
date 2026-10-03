using MarkItDownWeb.Application.Services;
using MarkItDownWeb.Application.DTOs;
using MarkItDownWeb.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MarkItDownWeb.Web.Controllers;

public class ConversionController : Controller
{
    private readonly ConversionService _conversionService;
    private readonly LibraryService _libraryService;

    public ConversionController(ConversionService conversionService, LibraryService libraryService)
    {
        _conversionService = conversionService;
        _libraryService = libraryService;
    }

    [HttpGet]
    public IActionResult Index() => View(new ConversionViewModel());

    [HttpPost]
    [RequestSizeLimit(105_906_176)]
    public async Task<IActionResult> Convert(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was received." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _conversionService.ConvertAsync(stream, file.FileName, file.Length, ct);

            return Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> History(string? search, DateTime? date, CancellationToken ct)
    {
        var items = await _conversionService.GetHistoryAsync(search, date, ct);
        return Json(items);
    }

    [HttpGet]
    public async Task<IActionResult> Item(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _conversionService.GetHistoryItemAsync(id, ct);
            return Json(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Conversion not found." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? query, CancellationToken ct)
        => Json(await _libraryService.SearchAsync(query, ct));

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] LibraryQuestionRequest request, CancellationToken ct)
        => Json(await _libraryService.AskAsync(request.Question, ct));

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _conversionService.DeleteAsync(id, ct);
        return Ok();
    }
}

public sealed record LibraryQuestionRequest(string? Question);
