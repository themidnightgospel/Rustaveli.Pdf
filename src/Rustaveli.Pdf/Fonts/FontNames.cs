namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The names a font gives itself, from its <c>name</c> table.
/// </summary>
/// <remarks>
/// Fonts name their family twice. The legacy family (name ID 1) can hold only four styles — regular, bold, italic,
/// bold italic — so a family with a semibold or a condensed face splits it off as "Family SemiBold". The
/// typographic family (ID 16) keeps every face under one name. Matching prefers the typographic name, which is how a
/// semibold face is found by asking for the family at weight 600.
/// </remarks>
internal sealed class FontNames
{
    public static readonly FontNames None =
        new FontNames(string.Empty, string.Empty, string.Empty, null, null, null, [], []);

    public FontNames(
        string family,
        string subfamily,
        string fullName,
        string? postScriptName,
        string? typographicFamily,
        string? typographicSubfamily,
        IReadOnlyList<string> familyAliases,
        IReadOnlyList<string> typographicFamilyAliases)
    {
        Family = family;
        Subfamily = subfamily;
        FullName = fullName;
        TypographicFamily = typographicFamily;
        TypographicSubfamily = typographicSubfamily;
        FamilyAliases = familyAliases;
        TypographicFamilyAliases = typographicFamilyAliases;
        PostScriptName = SanitizePostScriptName(postScriptName, PreferredFamily, PreferredSubfamily);
    }

    /// <summary>Name ID 1, the legacy family; empty when the font has none.</summary>
    public string Family { get; }

    /// <summary>Name ID 2, the legacy subfamily ("Regular", "Bold Italic"); empty when the font has none.</summary>
    public string Subfamily { get; }

    /// <summary>Name ID 4; empty when the font has none.</summary>
    public string FullName { get; }

    /// <summary>
    /// Name ID 6, the name a PDF records as the font's BaseFont. Always usable in PDF: a missing name is derived from
    /// the family, and characters PostScript names forbid are removed.
    /// </summary>
    public string PostScriptName { get; }

    /// <summary>Name ID 16, or null when the legacy family already is the typographic one.</summary>
    public string? TypographicFamily { get; }

    /// <summary>Name ID 17, or null when the legacy subfamily already is the typographic one.</summary>
    public string? TypographicSubfamily { get; }

    /// <summary>The family to match and display: the typographic family where the font has one.</summary>
    public string PreferredFamily => TypographicFamily ?? Family;

    /// <summary>The style within <see cref="PreferredFamily"/>.</summary>
    public string PreferredSubfamily => TypographicSubfamily ?? Subfamily;

    /// <summary>The legacy family (ID 1) in every language the font is localised into.</summary>
    /// <remarks>A document written in Japanese may well name a font by its Japanese family name.</remarks>
    public IReadOnlyList<string> FamilyAliases { get; }

    /// <summary>The typographic family (ID 16) in every language; empty when the font has none.</summary>
    public IReadOnlyList<string> TypographicFamilyAliases { get; }

    /// <summary>
    /// Whether this is a last-resort font — Apple's LastResort, or the Unicode Consortium's Last Resort it derives
    /// from — which claims every code point but draws each as a box naming its block. Set in one, text loses its
    /// letters, and every character of a block shares a glyph, so it cannot be read back out of the PDF either.
    /// </summary>
    public bool IsLastResort =>
        string.Equals(PreferredFamily.Replace(" ", string.Empty), "LastResort", StringComparison.OrdinalIgnoreCase);

    /// <summary>The names of <see cref="PreferredFamily"/> in every language.</summary>
    public IReadOnlyList<string> PreferredFamilyAliases =>
        TypographicFamilyAliases.Count > 0 ? TypographicFamilyAliases : FamilyAliases;

    /// <summary>
    /// A PostScript name for the font: printable ASCII other than the delimiters PostScript and PDF names reserve,
    /// at most 63 characters.
    /// </summary>
    private static string SanitizePostScriptName(string? name, string family, string subfamily)
    {
        string source = !string.IsNullOrEmpty(name) ? name!
            : subfamily.Length > 0 ? family + "-" + subfamily
            : family;
        char[] kept = new char[Math.Min(source.Length, 63)];
        int length = 0;

        foreach (char character in source)
        {
            if (length == kept.Length)
                break;

            if (character is > ' ' and < (char)127 && "[](){}<>/%".IndexOf(character) < 0)
                kept[length++] = character;
        }

        return length == 0 ? "Font" : new string(kept, 0, length);
    }
}
