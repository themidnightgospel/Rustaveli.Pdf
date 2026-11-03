namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Pair kerning: how much to tighten or loosen the space between two particular glyphs.
/// </summary>
internal abstract class KerningSource
{
    /// <summary>
    /// More pair subtables than any real font carries. Measuring consults every one of them for every pair, so an
    /// adversarial font listing a subtable hundreds of thousands of times would make each measurement crawl.
    /// </summary>
    protected const int MaximumSubtables = 4096;

    /// <summary>
    /// The adjustment to the advance of <paramref name="left"/> when <paramref name="right"/> follows it, in font
    /// units; negative brings the pair closer. Zero when the font does not kern the pair.
    /// </summary>
    public abstract int GetAdjustment(ushort left, ushort right);
}
