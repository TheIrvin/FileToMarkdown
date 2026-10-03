using System.Text.RegularExpressions;
using MarkItDownWeb.Application.DTOs;
using MarkItDownWeb.Application.Interfaces;

namespace MarkItDownWeb.Application.Services;

public sealed partial class LibraryService
{
    private const int MaxQueryLength = 300;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "do", "does", "for", "from", "how", "in", "is", "it", "of", "on", "or", "the", "to", "was", "were", "what", "when", "where", "which", "who", "why",
        "con", "cuál", "cuáles", "cómo", "de", "del", "el", "ella", "en", "es", "esta", "este", "la", "las", "lo", "los", "más", "para", "por", "qué", "se", "sobre", "son", "un", "una", "y"
    };
    private readonly IConversionRepository _repository;
    private readonly IAnswerGenerator _answerGenerator;

    public LibraryService(IConversionRepository repository, IAnswerGenerator answerGenerator)
    {
        _repository = repository;
        _answerGenerator = answerGenerator;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex TermRegex();

    public async Task<IReadOnlyList<LibrarySearchHit>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var terms = GetTerms(query);
        return terms.Length == 0
            ? []
            : await _repository.SearchContentAsync(terms, 20, ct);
    }

    public async Task<LibraryAnswerDto> AskAsync(string? question, CancellationToken ct = default)
    {
        var terms = GetTerms(question);
        if (terms.Length == 0)
            return _answerGenerator.CreateAnswer([]);

        var evidence = await _repository.SearchContentAsync(terms, 8, ct);
        return _answerGenerator.CreateAnswer(evidence);
    }

    private static string[] GetTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        return TermRegex().Matches(query[..Math.Min(query.Length, MaxQueryLength)])
            .Select(match => match.Value)
            .Where(term => !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }
}
