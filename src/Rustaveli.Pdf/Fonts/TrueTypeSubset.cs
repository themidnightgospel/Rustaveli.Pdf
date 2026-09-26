namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// A subset TrueType font program and how its glyphs are numbered relative to the font it came from.
/// </summary>
/// <remarks>
/// Embedded as a CIDFontType2 with Identity-H encoding, the subset's glyph ids serve directly as CIDs: content
/// streams show subset glyph ids, the CIDToGIDMap is /Identity, and /W widths and the ToUnicode CMap are keyed by
/// subset glyph id. The original font's advances and characters are found through <see cref="OriginalGlyphIds"/>.
/// </remarks>
internal sealed class TrueTypeSubset
{
    private readonly ushort[] _originalGlyphIds;
    private readonly Dictionary<ushort, ushort> _subsetGlyphIds;

    internal TrueTypeSubset(byte[] fontData, ushort[] originalGlyphIds, string tag)
    {
        FontData = fontData;
        _originalGlyphIds = originalGlyphIds;
        Tag = tag;
        _subsetGlyphIds = new Dictionary<ushort, ushort>(originalGlyphIds.Length);

        for (int index = 0; index < originalGlyphIds.Length; index++)
            _subsetGlyphIds.Add(originalGlyphIds[index], (ushort)index);
    }

    /// <summary>The subset font file, for a FontFile2 stream.</summary>
    public byte[] FontData { get; }

    public int GlyphCount => _originalGlyphIds.Length;

    /// <summary>For each subset glyph id, the glyph it was in the original font. Entry 0 is always .notdef.</summary>
    public IReadOnlyList<ushort> OriginalGlyphIds => _originalGlyphIds;

    /// <summary>Original glyph id to subset glyph id, for every glyph the subset contains.</summary>
    public IReadOnlyDictionary<ushort, ushort> GlyphIdMap => _subsetGlyphIds;

    /// <summary>
    /// Six capital letters identifying this subset, which PDF requires before the name of a subset font
    /// ("ABCDEF+NotoSans-Regular"). Derived from the font's name and the glyphs kept, so the same document produces
    /// the same tag on every run and every machine.
    /// </summary>
    public string Tag { get; }

    public bool TryGetSubsetGlyphId(ushort originalGlyphId, out ushort subsetGlyphId) =>
        _subsetGlyphIds.TryGetValue(originalGlyphId, out subsetGlyphId);
}
