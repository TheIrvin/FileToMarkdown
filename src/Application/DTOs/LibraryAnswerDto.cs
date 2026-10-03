namespace MarkItDownWeb.Application.DTOs;

public sealed class LibraryAnswerDto
{
    public string Answer { get; init; } = string.Empty;
    public bool HasEvidence { get; init; }
    public IReadOnlyList<LibrarySearchHit> Citations { get; init; } = [];
}
