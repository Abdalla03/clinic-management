using System.Globalization;
using System.Text;

namespace ClinicManagement.Domain.Common;

public static class SubstanceNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            var mapped = MapArabicVariant(ch);
            if (mapped is null)
            {
                continue;
            }

            var current = mapped.Value;

            if (char.IsLetterOrDigit(current) || current is ' ' or '-' or '+')
            {
                builder.Append(current);
                continue;
            }

            builder.Append(' ');
        }

        var collapsed = new StringBuilder(builder.Length);
        var previousWasSpace = false;

        foreach (var ch in builder.ToString())
        {
            if (ch == ' ')
            {
                if (!previousWasSpace)
                {
                    collapsed.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            collapsed.Append(ch);
            previousWasSpace = false;
        }

        return collapsed.ToString().Trim();
    }

    private static char? MapArabicVariant(char ch)
    {
        return ch switch
        {
            '\u0640' => null,
            '\u0622' => '\u0627',
            '\u0623' => '\u0627',
            '\u0625' => '\u0627',
            '\u0671' => '\u0627',
            '\u0629' => '\u0647',
            '\u0649' => '\u064A',
            '\u06CC' => '\u064A',
            '\u0624' => '\u0648',
            '\u0626' => '\u064A',
            _ => char.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark ? null : ch
        };
    }
}