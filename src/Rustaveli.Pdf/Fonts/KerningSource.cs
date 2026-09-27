namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Pair kerning: how much to tighten or loosen the space between two particular glyphs.
/// </summary>
internal abstract class KerningSource
{
    /// <summary>
    /// The adjustment to the advance of <paramref name="left"/> when <paramref name="right"/> follows it, in font
    /// units; negative brings the pair closer. Zero when the font does not kern the pair.
    /// </summary>
    public abstract int GetAdjustment(ushort left, ushort right);
}
