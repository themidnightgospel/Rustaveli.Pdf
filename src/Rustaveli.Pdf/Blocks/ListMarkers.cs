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
    public static string Format(ListNumbering numbering, int oneBasedIndex) => numbering switch
    {
        ListNumbering.Arabic => Numerals.Arabic(oneBasedIndex) + ".",

        // Letters have nothing before A, so a list numbered from zero or below starts there.
        ListNumbering.LowerAlpha => Numerals.LowerAlpha(Math.Max(1, oneBasedIndex)) + ".",
        ListNumbering.UpperAlpha => Numerals.UpperAlpha(Math.Max(1, oneBasedIndex)) + ".",
        ListNumbering.LowerRoman => Numerals.LowerRoman(oneBasedIndex) + ".",
        ListNumbering.UpperRoman => Numerals.UpperRoman(oneBasedIndex) + ".",
        _ => "•",
    };
}
