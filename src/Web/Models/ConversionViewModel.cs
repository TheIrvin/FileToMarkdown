namespace MarkItDownWeb.Web.Models;

public class ConversionViewModel
{
    public Guid? ConversionId { get; set; }
    public string? FileName { get; set; }
    public string? Markdown { get; set; }
    public string? ErrorMessage { get; set; }
}
