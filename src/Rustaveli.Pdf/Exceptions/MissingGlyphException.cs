using System.Globalization;

namespace Rustaveli.Pdf;

/// <summary>
/// Thrown, when an export asks for every glyph, if the document holds characters no typeface has. In print such
/// a character is a <em>missing glyph</em>, which a layout application highlights rather than let go to press.
/// </summary>
/// <remarks>
/// Without the check, a missing character is drawn as the face's missing-glyph box and the export succeeds.
/// </remarks>
public sealed class MissingGlyphException : TypesettingException
{
    private const int Listed = 20;

    internal MissingGlyphException(IEnumerable<int> codepoints)
        : this(codepoints.Distinct().OrderBy(codepoint => codepoint).ToArray())
    {
    }

    private MissingGlyphException(int[] codepoints)
        : base(Describe(codepoints), null)
    {
        Characters = codepoints.Select(char.ConvertFromUtf32).ToArray();
    }

    /// <summary>The characters no typeface has, each once, in code point order.</summary>
    public IReadOnlyList<string> Characters { get; }

    private static string Describe(int[] codepoints)
    {
        string listed = string.Join(", ", codepoints.Take(Listed).Select(codepoint =>
            string.Format(CultureInfo.InvariantCulture, "U+{0:X4} '{1}'", codepoint, char.ConvertFromUtf32(codepoint))));

        string more = codepoints.Length > Listed
            ? string.Format(CultureInfo.InvariantCulture, " and {0} more", codepoints.Length - Listed)
            : string.Empty;

        return $"No typeface has {(codepoints.Length == 1 ? "this character" : "these characters")}: {listed}{more}. "
            + "Register a typeface that has them with TypefaceLibrary, name one as a fallback, or leave "
            + "RequireEveryGlyph off to draw them as missing-glyph boxes.";
    }
}
