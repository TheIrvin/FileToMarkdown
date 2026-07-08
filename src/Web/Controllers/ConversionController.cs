using MarkItDownWeb.Application.Services;
using MarkItDownWeb.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MarkItDownWeb.Web.Controllers;

public class ConversionController : Controller
{
    private readonly ConversionService _conversionService;

    public ConversionController(ConversionService conversionService)
        => _conversionService = conversionService;

    [HttpGet]
    public IActionResult Index() => View(new ConversionViewModel());

    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Convert(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was received." });

        await using var stream = file.OpenReadStream();
        var result = await _conversionService.ConvertAsync(stream, file.FileName, file.Length, ct);

        return Json(result);
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
        var result = await _conversionService.GetHistoryItemAsync(id, ct);
        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _conversionService.DeleteAsync(id, ct);
        return Ok();
    }
}
