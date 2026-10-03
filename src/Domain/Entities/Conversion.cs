namespace MarkItDownWeb.Domain.Entities;

public class Conversion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public FileType FileType { get; set; }
    public string OriginalFilePath { get; set; } = string.Empty;
    public string? MarkdownFilePath { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double? ConversionTimeSeconds { get; set; }
    public ConversionStatus Status { get; set; } = ConversionStatus.Pending;
    public string? ErrorMessage { get; set; }
    public List<DocumentChunk> Chunks { get; set; } = [];

    public void MarkAsCompleted(string markdownFilePath, double elapsedSeconds)
    {
        MarkdownFilePath = markdownFilePath;
        ConversionTimeSeconds = elapsedSeconds;
        Status = ConversionStatus.Completed;
    }

    public void MarkAsFailed(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = ConversionStatus.Failed;
    }
}
