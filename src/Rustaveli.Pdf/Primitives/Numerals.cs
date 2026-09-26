using System.Globalization;
using System.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Writes numbers as the numerals folios and lists are set in: arabic, roman or alphabetic.
/// </summary>
/// <remarks>
/// Front matter is traditionally numbered in lower-case roman — i, ii, iii — and appendices lettered; pass one of
/// these to <see cref="TextComposer.Folio(Func{int, string})"/> to number pages that way.
/// </remarks>
public static class Numerals
{
    private static readonly (int Value, string Symbol)[] RomanValues =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    ];

    /// <summary>1, 2, 3, in the digits every culture reads.</summary>
    public static string Arabic(int number) => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>I, II, III … MMMCMXCIX. Roman numerals have no zero and nothing past 3999 is legible, so those stay arabic.</summary>
    public static string UpperRoman(int number)
    {
        if (number is <= 0 or > 3999)
            return Arabic(number);

        StringBuilder result = new StringBuilder();
        int remaining = number;

        foreach ((int value, string symbol) in RomanValues)
        {
            while (remaining >= value)
            {
                result.Append(symbol);
                remaining -= value;
            }
        }

        return result.ToString();
    }

    /// <summary>i, ii, iii, as front matter is numbered.</summary>
    public static string LowerRoman(int number) => UpperRoman(number).ToLowerInvariant();

    /// <summary>A, B … Z, AA, AB — as spreadsheet columns run, so the sequence never ends. Zero and below stay arabic.</summary>
    public static string UpperAlpha(int number)
    {
        if (number <= 0)
            return Arabic(number);

        StringBuilder result = new StringBuilder();
        int remaining = number;

        while (remaining > 0)
        {
            remaining--;
            result.Insert(0, (char)('A' + (remaining % 26)));
            remaining /= 26;
        }

        return result.ToString();
    }

    /// <summary>a, b … z, aa, ab.</summary>
    public static string LowerAlpha(int number) => UpperAlpha(number).ToLowerInvariant();

    /// <summary>The number in the style a list is numbered in; a bulleted list has no numerals, so a bullet.</summary>
    public static string Format(int number, ListNumbering numbering) => numbering switch
    {
        ListNumbering.Arabic => Arabic(number),
        ListNumbering.LowerAlpha => LowerAlpha(number),
        ListNumbering.UpperAlpha => UpperAlpha(number),
        ListNumbering.LowerRoman => LowerRoman(number),
        ListNumbering.UpperRoman => UpperRoman(number),
        _ => "•",
    };
}
