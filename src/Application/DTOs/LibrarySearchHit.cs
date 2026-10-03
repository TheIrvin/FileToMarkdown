namespace MarkItDownWeb.Application.DTOs;

public sealed class LibrarySearchHit
{
    public Guid ConversionId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int MatchCount { get; init; }
}
