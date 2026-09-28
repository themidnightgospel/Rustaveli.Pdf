namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The <c>CFF </c> table of a font with PostScript outlines, read as far as a PDF writer needs: the font's name,
/// whether it is CID-keyed, how many glyphs it has, and which CID each glyph carries.
/// </summary>
/// <remarks>
/// <para>
/// Charstrings are not interpreted, so glyph bounding boxes come from the font-wide box. A PDF embeds the font as a
/// subset where <see cref="CffSubsetter"/> can make one.
/// </para>
/// <para>
/// The CID matters when a CFF font is embedded as a PDF CIDFont. The PDF addresses glyphs by CID: in a name-keyed
/// font (most Latin fonts) that is the glyph index itself, but a CID-keyed font (most CJK fonts) numbers its glyphs
/// through its charset, and a PDF that wrote glyph indices there would show the wrong glyphs.
/// </para>
/// </remarks>
internal sealed class CompactFontTable
{
    private const int CharsetOperator = 15;
    private const int CharStringsOperator = 17;
    private const int RegistryOrderingSupplementOperator = (12 << 8) | 30;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _charset;
    private ushort[]? _cids;

    public CompactFontTable(ReadOnlyMemory<byte> data)
    {
        _data = data;
        ReadOnlySpan<byte> cff = data.Span;

        int major = BigEndian.UInt8(cff, 0);

        if (major != 1)
            throw new FontFormatException($"CFF version {major} is not supported.");

        CffIndex names = CffIndex.Read(cff, BigEndian.UInt8(cff, 2));
        CffIndex topDicts = CffIndex.Read(cff, names.End);

        if (names.Count == 0 || topDicts.Count == 0)
            throw new FontFormatException("The CFF table contains no font.");

        (int nameStart, int nameLength) = names.GetItem(cff, 0);
        FontName = MacRoman.Decode(cff.Slice(nameStart, nameLength));

        (int dictStart, int dictLength) = topDicts.GetItem(cff, 0);
        int charStrings = -1;
        _charset = 0;

        foreach (CffDictEntry entry in CffDict.Read(cff.Slice(dictStart, dictLength)))
        {
            switch (entry.Operator)
            {
                case CharStringsOperator:
                    charStrings = entry.Integer();
                    break;
                case CharsetOperator:
                    _charset = entry.Integer();
                    break;
                case RegistryOrderingSupplementOperator:
                    IsCidKeyed = true;
                    break;
            }
        }

        if (charStrings <= 0 || charStrings >= cff.Length)
            throw new FontFormatException("The CFF font has no charstrings.");

        GlyphCount = CffIndex.Read(cff, charStrings).Count;
    }

    /// <summary>The font's name from the CFF Name INDEX.</summary>
    public string FontName { get; }

    /// <summary>True for a CID-keyed font, whose glyphs a PDF must address by CID rather than index.</summary>
    public bool IsCidKeyed { get; }

    public int GlyphCount { get; }

    /// <summary>
    /// The CID a PDF CIDFont uses for a glyph: from the charset in a CID-keyed font, the glyph index otherwise.
    /// </summary>
    public ushort GetCid(ushort glyph)
    {
        if (glyph >= GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyph), glyph, $"The font has {GlyphCount} glyphs.");

        if (!IsCidKeyed)
            return glyph;

        // A race reads the charset twice into identical arrays; either may be kept, so no lock is needed.
        ushort[] cids = _cids ??= ReadCharset();
        return cids[glyph];
    }

    /// <summary>
    /// The CIDs of every glyph. Offsets 0 to 2 name predefined charsets, which only name-keyed fonts may use; a
    /// CID-keyed font that names one is treated as numbering its glyphs by index.
    /// </summary>
    private ushort[] ReadCharset()
    {
        ReadOnlySpan<byte> cff = _data.Span;
        ushort[] cids = new ushort[GlyphCount];

        if (_charset <= 2)
        {
            for (int glyph = 0; glyph < cids.Length; glyph++)
                cids[glyph] = (ushort)glyph;

            return cids;
        }

        int format = BigEndian.UInt8(cff, _charset);
        int position = _charset + 1;
        int next = 1;

        // Glyph 0 is always .notdef at CID 0, and is not listed. Every range covers at least one glyph, so the loop
        // is bounded by the glyph count however the charset is corrupted.
        while (next < cids.Length)
        {
            switch (format)
            {
                case 0:
                    cids[next++] = BigEndian.UInt16(cff, position);
                    position += 2;
                    break;

                case 1:
                case 2:
                    int first = BigEndian.UInt16(cff, position);
                    int left = format == 1 ? BigEndian.UInt8(cff, position + 2) : BigEndian.UInt16(cff, position + 2);
                    position += format == 1 ? 3 : 4;

                    for (int offset = 0; offset <= left && next < cids.Length; offset++)
                        cids[next++] = (ushort)(first + offset);

                    break;

                default:
                    throw new FontFormatException($"CFF charset format {format} does not exist.");
            }
        }

        return cids;
    }
}
