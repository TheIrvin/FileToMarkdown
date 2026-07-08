namespace MarkItDownWeb.Application.DTOs;

public class ConversionResultDto
{
    public Guid ConversionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
    public double ConversionTimeSeconds { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
