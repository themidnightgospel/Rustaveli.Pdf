using System.Text;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Formats the marker shown beside each list item.
/// </summary>
/// <remarks>
/// Markers are resolved once, when the list is composed, from the item's position in the list. That keeps
/// numbering stable no matter how the list is later broken across pages — a list continuing onto page two
/// resumes at the right number rather than restarting.
/// </remarks>
internal static class ListMarkers
{
    public static string Format(ListNumbering marker, int oneBasedIndex) => marker switch
    {
        ListNumbering.Bullet => "•",
        ListNumbering.Arabic => $"{oneBasedIndex}.",
        ListNumbering.LowerAlpha => $"{Alphabetic(oneBasedIndex).ToLowerInvariant()}.",
        ListNumbering.UpperAlpha => $"{Alphabetic(oneBasedIndex)}.",
        ListNumbering.LowerRoman => $"{Roman(oneBasedIndex).ToLowerInvariant()}.",
        ListNumbering.UpperRoman => $"{Roman(oneBasedIndex)}.",
        _ => "•"
    };

    /// <summary>Produces A, B … Z, AA, AB — spreadsheet-column style, so the sequence never runs out.</summary>
    private static string Alphabetic(int value)
    {
        string result = string.Empty;
        int remaining = Math.Max(1, value);

        while (remaining > 0)
        {
            remaining--;
            result = (char)('A' + remaining % 26) + result;
            remaining /= 26;
        }

        return result;
    }

    private static readonly (int Value, string Symbol)[] RomanNumerals =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    ];

    private static string Roman(int value)
    {
        // Roman numerals have no zero and nothing beyond a few thousand is legible, so fall back to digits.
        if (value <= 0 || value > 3999)
            return value.ToString();

        StringBuilder result = new System.Text.StringBuilder();
        int remaining = value;

        foreach ((int numeral, string? symbol) in RomanNumerals)
        {
            while (remaining >= numeral)
            {
                result.Append(symbol);
                remaining -= numeral;
            }
        }

        return result.ToString();
    }
}
