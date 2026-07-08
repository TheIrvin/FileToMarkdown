namespace MarkItDownWeb.Application.DTOs;

public class ConversionListItemDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
