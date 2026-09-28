using System.Globalization;
using System.Text;

namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Cuts a CFF font down to the glyphs a document uses, as a CID-keyed CFF whose CIDs are the codes the document
/// already shows them by, so a PDF embeds kilobytes instead of the whole font — sixteen megabytes, for a Chinese face.
/// </summary>
/// <remarks>
/// <para>
/// The subset is CID-keyed whatever the font was: a name-keyed font's glyphs keep their glyph indices as CIDs, a
/// CID-keyed font's their CIDs, so the codes written into content streams need no renumbering. Its registry and
/// ordering are Adobe and Identity, as the CIDFont that carries it says.
/// </para>
/// <para>
/// Each kept glyph has the subroutines it calls written out in place (<see cref="CharStringFlattener"/>), and the
/// subroutines themselves are dropped, global and local: the glyphs of a Chinese font share megabytes of them. Every
/// font dict is kept, with its private dict less its subroutines; a name-keyed font's private dict becomes the one
/// font dict of the subset. Offsets are written in five bytes each, so the layout is known before any is written.
/// </para>
/// <para>
/// A font that uses what cannot be carried over — a charstring type other than 2, a synthetic font, more than one font
/// in the table, or a glyph <see cref="CharStringFlattener"/> refuses — is not subset, and is embedded whole instead.
/// </para>
/// </remarks>
internal static class CffSubsetter
{
    private const int FontBBox = 5;
    private const int Charset = 15;
    private const int CharStrings = 17;
    private const int Private = 18;
    private const int Subrs = 19;
    private const int CharstringType = (12 << 8) | 6;
    private const int FontMatrix = (12 << 8) | 7;
    private const int SyntheticBase = (12 << 8) | 20;
    private const int RegistryOrderingSupplement = (12 << 8) | 30;
    private const int CidCount = (12 << 8) | 34;
    private const int FdArray = (12 << 8) | 36;
    private const int FdSelect = (12 << 8) | 37;

    /// <summary>The first string ID a font's own strings take; below it are CFF's standard strings.</summary>
    private const int FirstCustomString = 391;

    private static readonly byte[][] Strings = [Encoding.ASCII.GetBytes("Adobe"), Encoding.ASCII.GetBytes("Identity")];

    /// <summary>
    /// A subset of the CFF table <paramref name="table"/> holding .notdef as CID 0 and each glyph of
    /// <paramref name="glyphs"/> as its CID; null when the font cannot be subset, and is to be embedded whole.
    /// </summary>
    /// <param name="table">The font's CFF table.</param>
    /// <param name="unitsPerEm">
    /// The font's units per em, from its <c>head</c> table: a CFF table with no font matrix of its own is scaled by
    /// it inside an OpenType font, but by a thousand once it stands alone, so the subset is given the matrix.
    /// </param>
    /// <param name="glyphs">The glyphs to keep, each with the CID it is to have.</param>
    public static byte[]? TrySubset(ReadOnlyMemory<byte> table, int unitsPerEm, IEnumerable<(ushort Cid, ushort Glyph)> glyphs)
    {
        try
        {
            return Subset(table.ToArray(), unitsPerEm, glyphs);
        }
        catch (Exception exception) when (exception is NotSupportedException or FontFormatException)
        {
            return null;
        }
    }

    private static byte[] Subset(byte[] cff, int unitsPerEm, IEnumerable<(ushort Cid, ushort Glyph)> glyphs)
    {
        if (cff.Length < 4 || cff[0] != 1)
            throw new NotSupportedException("Only CFF version 1 is subset.");

        CffIndex names = CffIndex.Read(cff, cff[2]);
        CffIndex topDicts = CffIndex.Read(cff, names.End);
        CffIndex globalSubrs = CffIndex.Read(cff, CffIndex.Read(cff, topDicts.End).End);

        if (names.Count != 1 || topDicts.Count != 1)
            throw new NotSupportedException("Only a CFF table holding one font is subset.");

        (int nameStart, int nameLength) = names.GetItem(cff, 0);
        (int topStart, int topLength) = topDicts.GetItem(cff, 0);
        byte[] top = cff.AsSpan(topStart, topLength).ToArray();
        List<CffDictEntry> topEntries = CffDict.Read(top);

        if (Find(topEntries, CharstringType) is { } type && type.Integer() != 2)
            throw new NotSupportedException("Only Type 2 charstrings are subset.");

        if (Find(topEntries, SyntheticBase) is not null)
            throw new NotSupportedException("A synthetic font is not subset.");

        CffIndex charStrings = CffIndex.Read(cff, Required(topEntries, CharStrings).Integer());
        List<FontDict> fonts = [];
        byte[]? fdSelect = null;

        if (Find(topEntries, RegistryOrderingSupplement) is not null)
        {
            CffIndex fdArray = CffIndex.Read(cff, Required(topEntries, FdArray).Integer());

            for (int index = 0; index < fdArray.Count; index++)
            {
                (int start, int length) = fdArray.GetItem(cff, index);
                byte[] dict = cff.AsSpan(start, length).ToArray();
                fonts.Add(ReadFontDict(cff, dict, CffDict.Read(dict)));
            }

            fdSelect = ReadFdSelect(cff, Required(topEntries, FdSelect).Integer(), charStrings.Count, fonts.Count);
        }
        else
        {
            // A name-keyed font's private dict becomes the one font dict of the subset, which says nothing else.
            fonts.Add(ReadFontDict(cff, [], [Required(topEntries, Private)]));
        }

        // .notdef first, as CID 0; then each glyph once, in CID order.
        List<(ushort Cid, ushort Glyph)> kept = [(0, 0)];
        kept.AddRange(glyphs.Where(glyph => glyph.Cid != 0).Distinct().OrderBy(glyph => glyph.Cid));

        if (kept.Select(glyph => glyph.Cid).Distinct().Count() != kept.Count)
            throw new ArgumentException("A CID can stand for only one glyph.", nameof(glyphs));

        List<byte[]> outlines = new List<byte[]>(kept.Count);
        byte[] selection = new byte[kept.Count];

        for (int index = 0; index < kept.Count; index++)
        {
            ushort glyph = kept[index].Glyph;

            if (glyph >= charStrings.Count)
                throw new FontFormatException($"The font has no glyph {glyph}.");

            selection[index] = fdSelect is null ? (byte)0 : fdSelect[glyph];
            (int start, int length) = charStrings.GetItem(cff, glyph);
            outlines.Add(CharStringFlattener.Flatten(cff, start, length, globalSubrs, fonts[selection[index]].LocalSubrs));
        }

        return Write(cff.AsSpan(nameStart, nameLength).ToArray(), top, topEntries, unitsPerEm, fonts, kept, outlines, selection);
    }

    /// <summary>
    /// A font dict as the subset keeps it: its font matrix alone, since its other entries name strings the subset
    /// does not carry or say nothing a PDF uses; its private dict less its subroutines; and those subroutines, to
    /// flatten its glyphs.
    /// </summary>
    private static FontDict ReadFontDict(byte[] cff, byte[] dict, List<CffDictEntry> entries)
    {
        CffDictEntry privateEntry = Required(entries, Private);
        int size = privateEntry.Integer(0);
        int offset = privateEntry.Integer(1);
        ReadOnlySpan<byte> privateDict = BigEndian.Slice(cff, offset, size);
        List<CffDictEntry> privateEntries = CffDict.Read(privateDict);

        CffIndex localSubrs = Find(privateEntries, Subrs) is { } subrs
            ? CffIndex.Read(cff, offset + subrs.Integer())
            : default;

        return new FontDict(
            Copy(dict, entries, op => op == FontMatrix),
            Copy(privateDict, privateEntries, op => op != Subrs),
            localSubrs);
    }

    /// <summary>The font dict of each glyph, from an FDSelect in format 0 or 3.</summary>
    private static byte[] ReadFdSelect(byte[] cff, int offset, int glyphCount, int fontCount)
    {
        byte[] selection = new byte[glyphCount];
        int format = BigEndian.UInt8(cff, offset);

        if (format == 0)
        {
            BigEndian.Slice(cff, offset + 1, glyphCount).CopyTo(selection);
        }
        else if (format == 3)
        {
            int ranges = BigEndian.UInt16(cff, offset + 1);

            for (int range = 0; range < ranges; range++)
            {
                int at = offset + 3 + (range * 3);
                int first = BigEndian.UInt16(cff, at);
                byte font = BigEndian.UInt8(cff, at + 2);
                int next = BigEndian.UInt16(cff, at + 3);

                for (int glyph = first; glyph < next && glyph < glyphCount; glyph++)
                    selection[glyph] = font;
            }
        }
        else
        {
            throw new FontFormatException($"FDSelect format {format} does not exist.");
        }

        if (selection.Any(font => font >= fontCount))
            throw new FontFormatException("FDSelect names a font dict that does not exist.");

        return selection;
    }

    private static byte[] Write(
        byte[] name,
        byte[] top,
        List<CffDictEntry> topEntries,
        int unitsPerEm,
        List<FontDict> fonts,
        List<(ushort Cid, ushort Glyph)> kept,
        List<byte[]> outlines,
        byte[] selection)
    {
        // The top dict's size does not depend on the offsets it holds, each written in five bytes, so it is laid out
        // once with zeros to be measured, then again with the offsets found.
        byte[] Top(int charset, int fdSelect, int charStrings, int fdArray)
        {
            FontDataWriter dict = new FontDataWriter();
            Integer(dict, FirstCustomString);
            Integer(dict, FirstCustomString + 1);
            Integer(dict, 0);
            Operator(dict, RegistryOrderingSupplement);

            foreach (CffDictEntry entry in topEntries.Where(entry => entry.Operator is FontBBox or FontMatrix))
                dict.Bytes(top.AsSpan(entry.Start, entry.Length));

            if (unitsPerEm != 1000 && Find(topEntries, FontMatrix) is null)
            {
                double scale = 1d / unitsPerEm;
                Real(dict, scale);
                Integer(dict, 0);
                Integer(dict, 0);
                Real(dict, scale);
                Integer(dict, 0);
                Integer(dict, 0);
                Operator(dict, FontMatrix);
            }

            Integer(dict, kept.Max(glyph => glyph.Cid) + 1);
            Operator(dict, CidCount);
            Offset(dict, charset);
            Operator(dict, Charset);
            Offset(dict, fdSelect);
            Operator(dict, FdSelect);
            Offset(dict, charStrings);
            Operator(dict, CharStrings);
            Offset(dict, fdArray);
            Operator(dict, FdArray);
            return dict.ToArray();
        }

        byte[] charset = Charset0(kept);
        byte[] fdSelect = [0, .. selection];
        byte[][] names = [name];

        int topSize = Top(0, 0, 0, 0).Length;
        int charsetAt = 4 + IndexSize(names) + IndexSize([new byte[topSize]]) + IndexSize(Strings) + IndexSize([]);
        int fdSelectAt = charsetAt + charset.Length;
        int charStringsAt = fdSelectAt + fdSelect.Length;
        int fdArrayAt = charStringsAt + IndexSize(outlines);

        // Each font dict ends with its private dict's size and offset, the private dicts following the FDArray.
        byte[][] fontDicts = new byte[fonts.Count][];
        int privateAt = fdArrayAt + IndexSize(fonts.Select(font => new byte[font.Dict.Length + 11]).ToList());

        for (int index = 0; index < fonts.Count; index++)
        {
            FontDataWriter dict = new FontDataWriter();
            dict.Bytes(fonts[index].Dict);
            Offset(dict, fonts[index].Private.Length);
            Offset(dict, privateAt);
            Operator(dict, Private);
            fontDicts[index] = dict.ToArray();
            privateAt += fonts[index].Private.Length;
        }

        FontDataWriter file = new FontDataWriter();
        file.Bytes([1, 0, 4, 4]);
        WriteIndex(file, names);
        WriteIndex(file, [Top(charsetAt, fdSelectAt, charStringsAt, fdArrayAt)]);
        WriteIndex(file, Strings);
        WriteIndex(file, []);
        file.Bytes(charset);
        file.Bytes(fdSelect);
        WriteIndex(file, outlines);
        WriteIndex(file, fontDicts);

        foreach (FontDict font in fonts)
            file.Bytes(font.Private);

        return file.ToArray();
    }

    /// <summary>A format 0 charset: the CID of every glyph but .notdef, in glyph order.</summary>
    private static byte[] Charset0(List<(ushort Cid, ushort Glyph)> kept)
    {
        FontDataWriter charset = new FontDataWriter();
        charset.UInt8(0);

        foreach ((ushort cid, _) in kept.Skip(1))
            charset.UInt16(cid);

        return charset.ToArray();
    }

    /// <summary>The entries of <paramref name="dict"/> whose operators it keeps, exactly as they were written.</summary>
    private static byte[] Copy(ReadOnlySpan<byte> dict, List<CffDictEntry> entries, Func<int, bool> keeps)
    {
        FontDataWriter copy = new FontDataWriter();

        foreach (CffDictEntry entry in entries.Where(entry => keeps(entry.Operator)))
            copy.Bytes(dict.Slice(entry.Start, entry.Length));

        return copy.ToArray();
    }

    private static CffDictEntry? Find(List<CffDictEntry> entries, int op) => entries.LastOrDefault(entry => entry.Operator == op);

    private static CffDictEntry Required(List<CffDictEntry> entries, int op) =>
        Find(entries, op) ?? throw new FontFormatException($"The CFF font lacks DICT operator {op}.");

    /// <summary>An integer in a DICT, in as few bytes as its value allows.</summary>
    private static void Integer(FontDataWriter dict, int value)
    {
        switch (value)
        {
            case >= -107 and <= 107:
                dict.UInt8(value + 139);
                break;
            case >= 108 and <= 1131:
                dict.UInt8(((value - 108) >> 8) + 247);
                dict.UInt8((value - 108) & 0xFF);
                break;
            case >= -1131 and <= -108:
                dict.UInt8(((-value - 108) >> 8) + 251);
                dict.UInt8((-value - 108) & 0xFF);
                break;
            case >= short.MinValue and <= short.MaxValue:
                dict.UInt8(28);
                dict.Int16(value);
                break;
            default:
                Offset(dict, value);
                break;
        }
    }

    /// <summary>
    /// A real number in a DICT, packed in nibbles as its round-tripping decimal is written: a positive one below one,
    /// such as a font matrix scales by, whose exponent, if written, is negative.
    /// </summary>
    private static void Real(FontDataWriter dict, double value)
    {
        // Seventeen digits round-trip on every runtime; "R" does not on .NET Framework.
        string text = value.ToString("G17", CultureInfo.InvariantCulture);
        List<int> nibbles = new List<int>(text.Length + 2);

        for (int index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '.':
                    nibbles.Add(0xA);
                    break;
                case 'E':
                    // Written "E-05": the minus is part of the exponent's one nibble.
                    nibbles.Add(0xC);
                    index++;
                    break;
                default:
                    nibbles.Add(text[index] - '0');
                    break;
            }
        }

        nibbles.Add(0xF);

        if (nibbles.Count % 2 == 1)
            nibbles.Add(0xF);

        dict.UInt8(30);

        for (int index = 0; index < nibbles.Count; index += 2)
            dict.UInt8((nibbles[index] << 4) | nibbles[index + 1]);
    }

    /// <summary>An integer in five bytes, however small, so a DICT's size does not depend on the offsets in it.</summary>
    private static void Offset(FontDataWriter dict, int value)
    {
        dict.UInt8(29);
        dict.UInt32((uint)value);
    }

    private static void Operator(FontDataWriter dict, int op)
    {
        if (op > 0xFF)
            dict.UInt8(op >> 8);

        dict.UInt8(op & 0xFF);
    }

    private static int IndexSize(IReadOnlyList<byte[]> items)
    {
        if (items.Count == 0)
            return 2;

        int data = items.Sum(item => item.Length);
        return 3 + ((items.Count + 1) * OffsetSize(data + 1)) + data;
    }

    private static void WriteIndex(FontDataWriter file, IReadOnlyList<byte[]> items)
    {
        file.UInt16(items.Count);

        if (items.Count == 0)
            return;

        int size = OffsetSize(items.Sum(item => item.Length) + 1);
        file.UInt8(size);
        uint offset = 1;
        file.UIntOfSize(offset, size);

        foreach (byte[] item in items)
        {
            offset += (uint)item.Length;
            file.UIntOfSize(offset, size);
        }

        foreach (byte[] item in items)
            file.Bytes(item);
    }

    private static int OffsetSize(int largest) => largest <= 0xFF ? 1 : largest <= 0xFFFF ? 2 : largest <= 0xFFFFFF ? 3 : 4;

    /// <summary>A font dict as the subset writes it, with its private dict and the subroutines its glyphs call.</summary>
    private sealed record FontDict(byte[] Dict, byte[] Private, CffIndex LocalSubrs);
}
