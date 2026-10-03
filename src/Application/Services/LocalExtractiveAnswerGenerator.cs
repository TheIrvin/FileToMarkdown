using MarkItDownWeb.Application.DTOs;
using MarkItDownWeb.Application.Interfaces;

namespace MarkItDownWeb.Application.Services;

public sealed class LocalExtractiveAnswerGenerator : IAnswerGenerator
{
    public LibraryAnswerDto CreateAnswer(IReadOnlyList<LibrarySearchHit> evidence)
    {
        if (evidence.Count == 0)
        {
            return new LibraryAnswerDto
            {
                Answer = "No encontré contenido que respalde una respuesta en los documentos de la biblioteca.",
                HasEvidence = false
            };
        }

        var citations = evidence.Take(4).ToArray();
        return new LibraryAnswerDto
        {
            Answer = $"El fragmento más relevante dice: «{citations[0].Content}»",
            HasEvidence = true,
            Citations = citations
        };
    }
}
