using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// A font assembled in memory from hand-built tables, for structures the committed fonts do not contain: every
/// character map format, both kern table layouts, class-based kerning, truncated tables and so on.
/// </summary>
internal sealed class SyntheticFont
{
    private readonly SortedDictionary<string, byte[]> _tables = new(StringComparer.Ordinal);

    public string Version { get; set; } = "\0\u0001\0\0";

    /// <summary>
    /// Three glyphs — .notdef, "A" and "B" — with advances 500, 600 and 700 at 1000 units per em, mapped by a format
    /// 4 character map, and TrueType outlines.
    /// </summary>
    public static SyntheticFont Minimal()
    {
        SyntheticFont font = new SyntheticFont();
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(
            longOffsets: false,
            SyntheticTables.SimpleGlyph(50, 0, 450, 700),
            SyntheticTables.SimpleGlyph(0, 0, 600, 700),
            SyntheticTables.SimpleGlyph(60, -10, 640, 710));

        return font
            .With("head", SyntheticTables.Head())
            .With("hhea", SyntheticTables.Hhea(numberOfHMetrics: 3))
            .With("maxp", SyntheticTables.Maxp(3))
            .With("hmtx", SyntheticTables.Hmtx((500, 50), (600, 0), (700, 60)))
            .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(('A', 1), ('B', 2)))))
            .With("glyf", glyf)
            .With("loca", loca);
    }

    /// <summary>A minimal font carrying a family name and a style, for matching tests.</summary>
    public static SyntheticFont Named(string family, int weight = 400, int width = 5, int fsSelection = 0x40)
    {
        string subfamily = (fsSelection & 1) != 0 ? "Italic" : "Regular";

        return Minimal()
            .With("name", SyntheticTables.Name(
                SyntheticTables.NameRecord(3, 1, 0x0409, 1, family),
                SyntheticTables.NameRecord(3, 1, 0x0409, 2, subfamily),
                SyntheticTables.NameRecord(3, 1, 0x0409, 4, family + " " + subfamily)))
            .With("OS/2", SyntheticTables.Os2(weight: weight, width: width, fsSelection: fsSelection));
    }

    public SyntheticFont With(string tag, byte[] data)
    {
        _tables[tag] = data;
        return this;
    }

    public SyntheticFont Without(string tag)
    {
        _tables.Remove(tag);
        return this;
    }

    public byte[] Build() => BuildAt(0);

    public OpenTypeFont Load() => OpenTypeFont.Load(Build());

    /// <summary>The font as it would sit at <paramref name="baseOffset"/> in a collection file.</summary>
    public byte[] BuildAt(int baseOffset)
    {
        FontBytes file = new FontBytes().Tag(Version).U16(_tables.Count).U16(0).U16(0).U16(0);
        int offset = baseOffset + 12 + (16 * _tables.Count);

        foreach ((string tag, byte[] data) in _tables)
        {
            file.Tag(tag).U32(0).U32(offset).U32(data.Length);
            offset += (data.Length + 3) & ~3;
        }

        foreach (byte[] data in _tables.Values)
            file.Bytes(data).Align(4);

        return file.ToArray();
    }

    /// <summary>A collection file holding the fonts in order.</summary>
    public static byte[] Collection(params SyntheticFont[] fonts)
    {
        FontBytes file = new FontBytes().Tag("ttcf").U16(1).U16(0).U32(fonts.Length);
        int offset = 12 + (4 * fonts.Length);
        List<byte[]> bodies = [];

        foreach (SyntheticFont font in fonts)
        {
            byte[] body = font.BuildAt(offset);
            file.U32(offset);
            bodies.Add(body);
            offset += body.Length;
        }

        foreach (byte[] body in bodies)
            file.Bytes(body);

        return file.ToArray();
    }
}
