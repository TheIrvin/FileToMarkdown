using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkItDownWeb.Application.Services;

public static partial class SearchTextNormalizer
{
    [GeneratedRegex(@"[\p{L}\p{N}]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    public static string NormalizeTerm(string value) => Normalize(value);

    public static string CreateIndex(params string?[] values)
    {
        var tokens = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => WordRegex().Matches(Normalize(value!)).Select(match => match.Value));

        return $" {string.Join(' ', tokens)} ";
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            builder.Append(Rune.ToLowerInvariant(rune).ToString());
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
