using System.Text.RegularExpressions;
using MarkItDownWeb.Domain.Entities;

namespace MarkItDownWeb.Application.Services;

internal static partial class MarkdownChunker
{
    private const int MaxChunkLength = 1200;

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"(?:<!--\s*PageNumber\s*=\s*""?(\d+)""?\s*-->|^\s*#{1,6}\s+(?:Page|Página)\s+(\d+)\s*$)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PageRegex();

    public static IReadOnlyList<DocumentChunk> Create(Guid conversionId, string markdown)
    {
        var chunks = new List<DocumentChunk>();
        var headings = new List<(int Level, string Text)>();
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var buffer = new List<string>();
        var section = "Contenido";
        string? page = null;
        var ordinal = 0;

        void Flush()
        {
            var text = string.Join('\n', buffer).Trim();
            buffer.Clear();
            if (text.Length == 0)
                return;

            foreach (var part in SplitText(text, MaxChunkLength))
            {
                chunks.Add(new DocumentChunk
                {
                    ConversionId = conversionId,
                    Ordinal = ordinal++,
                    Section = section.Length <= 500 ? section : section[..500],
                    Content = part
                });
            }
        }

        foreach (var line in lines)
        {
            var pageMatch = PageRegex().Match(line);
            if (pageMatch.Success)
            {
                Flush();
                var pageNumber = pageMatch.Groups[1].Success ? pageMatch.Groups[1].Value : pageMatch.Groups[2].Value;
                page = $"Página {pageNumber}";
                headings.Clear();
                section = page;
                continue;
            }

            var headingMatch = HeadingRegex().Match(line);
            if (headingMatch.Success)
            {
                Flush();
                var level = headingMatch.Groups[1].Length;
                var title = headingMatch.Groups[2].Value.Trim();
                while (headings.Count > 0 && headings[^1].Level >= level)
                    headings.RemoveAt(headings.Count - 1);
                headings.Add((level, title));
                var headingPath = string.Join(" › ", headings.Select(item => item.Text));
                section = page is null ? headingPath : $"{page} › {headingPath}";
                continue;
            }

            if (string.IsNullOrWhiteSpace(line) && buffer.Count > 0)
            {
                Flush();
                continue;
            }

            buffer.Add(line);
        }

        Flush();
        return chunks;
    }

    private static IEnumerable<string> SplitText(string text, int maxLength)
    {
        while (text.Length > maxLength)
        {
            var splitAt = text.LastIndexOf(' ', maxLength);
            if (splitAt < maxLength / 2)
                splitAt = maxLength;

            yield return text[..splitAt].Trim();
            text = text[splitAt..].Trim();
        }

        if (text.Length > 0)
            yield return text;
    }
}
