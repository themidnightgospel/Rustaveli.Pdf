namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>The three sequences of a contextual rule, which class-based rules match against separate classes.</summary>
internal enum ContextPart
{
    /// <summary>The glyphs before the input, matched from the nearest back.</summary>
    Backtrack,

    /// <summary>The glyphs the rule's lookups apply to.</summary>
    Input,

    /// <summary>The glyphs after the input.</summary>
    Lookahead
}
