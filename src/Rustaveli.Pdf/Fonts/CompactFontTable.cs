namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The <c>CFF </c> table of a font with PostScript outlines, read as far as a PDF writer needs: the font's name,
/// whether it is CID-keyed, how many glyphs it has, and which CID each glyph carries.
/// </summary>
/// <remarks>
/// <para>
/// Charstrings are not interpreted, so glyph bounding boxes come from the font-wide box, and CFF fonts are embedded
/// whole rather than subset.
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

    /// <summary>The operand stack depth CFF allows in a DICT.</summary>
    private const int MaximumOperands = 48;

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

        foreach ((int op, double operand) in ReadDict(cff.Slice(dictStart, dictLength)))
        {
            switch (op)
            {
                case CharStringsOperator:
                    charStrings = (int)operand;
                    break;
                case CharsetOperator:
                    _charset = (int)operand;
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

    /// <summary>
    /// The operators of a DICT with the last operand before each, which is all the operators read here need.
    /// </summary>
    private static List<(int Operator, double Operand)> ReadDict(ReadOnlySpan<byte> dict)
    {
        List<(int, double)> entries = new List<(int, double)>();
        int operands = 0;
        double last = 0;
        int position = 0;

        while (position < dict.Length)
        {
            int b0 = dict[position++];

            if (b0 <= 21)
            {
                int op = b0 == 12 ? (12 << 8) | BigEndian.UInt8(dict, position++) : b0;
                entries.Add((op, last));
                operands = 0;
                continue;
            }

            if (++operands > MaximumOperands)
                throw new FontFormatException("A CFF DICT has more operands than CFF allows.");

            switch (b0)
            {
                case 28:
                    last = BigEndian.Int16(dict, position);
                    position += 2;
                    break;
                case 29:
                    last = BigEndian.Int32(dict, position);
                    position += 4;
                    break;
                case 30:
                    // A real number, packed in nibbles up to the first 0xF. Its value matters to none of the
                    // operators read here, only where it ends.
                    byte packed;

                    do
                    {
                        packed = BigEndian.UInt8(dict, position++);
                    }
                    while ((packed >> 4) != 0x0F && (packed & 0x0F) != 0x0F);

                    last = 0;
                    break;
                case >= 32 and <= 246:
                    last = b0 - 139;
                    break;
                case >= 247 and <= 250:
                    last = ((b0 - 247) * 256) + BigEndian.UInt8(dict, position++) + 108;
                    break;
                case >= 251 and <= 254:
                    last = -((b0 - 251) * 256) - BigEndian.UInt8(dict, position++) - 108;
                    break;
                default:
                    throw new FontFormatException($"Byte {b0} does not begin a CFF DICT operand.");
            }
        }

        return entries;
    }
}
