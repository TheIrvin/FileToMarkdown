using MarkItDownWeb.Application.DTOs;

namespace MarkItDownWeb.Application.Interfaces;

public interface IAnswerGenerator
{
    LibraryAnswerDto CreateAnswer(IReadOnlyList<LibrarySearchHit> evidence);
}
