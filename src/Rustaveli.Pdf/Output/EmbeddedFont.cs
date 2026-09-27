using System.Globalization;
using System.Text;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Text;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// One face as a document embeds it: a Type 0 font over a CID font, shown with two-byte codes.
/// </summary>
/// <remarks>
/// <para>
/// TrueType faces are subset to the glyphs the document uses, numbered in order of first use: that number is the
/// code a content stream shows and, with <c>/CIDToGIDMap /Identity</c>, the glyph's index in the subset. The font
/// is written when the document finishes, once every glyph is known.
/// </para>
/// <para>
/// CFF faces are embedded whole, as OpenType, and shown by glyph index — or by CID for a CID-keyed font, which is
/// how PDF addresses its glyphs.
/// </para>
/// <para>
/// Every glyph remembers the character it was first used for, written out as a ToUnicode map so the text can be
/// searched, copied and read aloud.
/// </para>
/// </remarks>
internal sealed class EmbeddedFont
{
    private static readonly PdfName FontDescriptor = new PdfName("FontDescriptor");
    private static readonly PdfName FontName = new PdfName("FontName");
    private static readonly PdfName Flags = new PdfName("Flags");
    private static readonly PdfName FontBBox = new PdfName("FontBBox");
    private static readonly PdfName ItalicAngle = new PdfName("ItalicAngle");
    private static readonly PdfName Ascent = new PdfName("Ascent");
    private static readonly PdfName Descent = new PdfName("Descent");
    private static readonly PdfName CapHeight = new PdfName("CapHeight");
    private static readonly PdfName XHeight = new PdfName("XHeight");
    private static readonly PdfName StemV = new PdfName("StemV");
    private static readonly PdfName FontFile2 = new PdfName("FontFile2");
    private static readonly PdfName FontFile3 = new PdfName("FontFile3");
    private static readonly PdfName Length1 = new PdfName("Length1");
    private static readonly PdfName OpenType = new PdfName("OpenType");
    private static readonly PdfName BaseFont = new PdfName("BaseFont");
    private static readonly PdfName Type0 = new PdfName("Type0");
    private static readonly PdfName CidFontType0 = new PdfName("CIDFontType0");
    private static readonly PdfName CidFontType2 = new PdfName("CIDFontType2");
    private static readonly PdfName CidSystemInfo = new PdfName("CIDSystemInfo");
    private static readonly PdfName Registry = new PdfName("Registry");
    private static readonly PdfName Ordering = new PdfName("Ordering");
    private static readonly PdfName Supplement = new PdfName("Supplement");
    private static readonly PdfName CidToGidMap = new PdfName("CIDToGIDMap");
    private static readonly PdfName Identity = new PdfName("Identity");
    private static readonly PdfName Encoding = new PdfName("Encoding");
    private static readonly PdfName IdentityH = new PdfName("Identity-H");
    private static readonly PdfName DescendantFonts = new PdfName("DescendantFonts");
    private static readonly PdfName ToUnicode = new PdfName("ToUnicode");

    private readonly GlyphSubset? _subset;
    private readonly SortedDictionary<ushort, (ushort Glyph, string? Text)>? _shown;

    public EmbeddedFont(OpenTypeFont face, PdfReference reference)
    {
        Face = face;
        Reference = reference;

        if (face.Outlines == OutlineFormat.TrueType)
            _subset = new GlyphSubset(face);
        else
            _shown = new SortedDictionary<ushort, (ushort, string?)>();
    }

    public OpenTypeFont Face { get; }

    /// <summary>The Type 0 font, reserved when the face is first used and written when the document finishes.</summary>
    public PdfReference Reference { get; }

    /// <summary>The two-byte code a content stream shows <paramref name="glyph"/> with.</summary>
    public ushort CodeFor(ShapedGlyph glyph)
    {
        if (_subset is not null)
            return _subset.Add(glyph.Glyph, glyph.Codepoint, glyph.Text);

        CompactFontTable cff = Face.Cff!;
        ushort code = cff.IsCidKeyed ? cff.GetCid(glyph.Glyph) : glyph.Glyph;

        // As in a subset: the first text a glyph shows is kept, and .notdef, which stands for every missing character,
        // reads as none of them.
        bool known = _shown!.TryGetValue(code, out (ushort Glyph, string? Text) shown);

        if (known && !string.IsNullOrEmpty(shown.Text))
            return code;

        string? text = glyph.Glyph == 0 ? null : glyph.ReadsAs;
        _shown[code] = (glyph.Glyph, GlyphSubset.Keep(known ? shown.Text : null, text));

        return code;
    }

    public void Write(PdfFileWriter file)
    {
        if (_subset is not null)
            WriteTrueType(file, _subset);
        else
            WriteCompactFont(file, _shown!);
    }

    private void WriteTrueType(PdfFileWriter file, GlyphSubset subset)
    {
        TrueTypeSubset font = subset.Build();
        string name = font.Tag + "+" + PostScriptName(Face);

        PdfReference program = file.WriteStream(new PdfDictionary { [Length1] = font.FontData.Length }, font.FontData);

        PdfArray widths = new PdfArray(font.GlyphCount);
        foreach (ushort original in font.OriginalGlyphIds)
            widths.Add(Width(original));

        List<(ushort Code, string Text)> characters = [];
        for (int code = 0; code < font.GlyphCount; code++)
        {
            if (subset.TryGetText((ushort)code, out string text))
                characters.Add(((ushort)code, text));
        }

        PdfReference cidFont = file.Write(new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.Font,
            [PdfNames.Subtype] = CidFontType2,
            [BaseFont] = new PdfName(name),
            [CidSystemInfo] = SystemInfo(),
            [FontDescriptor] = WriteDescriptor(file, name, FontFile2, program),
            [PdfNames.W] = new PdfArray(2) { 0, widths },
            [CidToGidMap] = Identity,
        });

        WriteType0(file, name, cidFont, characters);
    }

    private void WriteCompactFont(PdfFileWriter file, SortedDictionary<ushort, (ushort Glyph, string? Text)> shown)
    {
        string name = PostScriptName(Face);

        PdfReference program = file.WriteStream(new PdfDictionary { [PdfNames.Subtype] = OpenType }, Face.ToStandaloneFile());

        PdfArray widths = new PdfArray(shown.Count * 2);
        foreach (KeyValuePair<ushort, (ushort Glyph, string? Text)> entry in shown)
        {
            widths.Add(entry.Key);
            widths.Add(new PdfArray(1) { Width(entry.Value.Glyph) });
        }

        PdfReference cidFont = file.Write(new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.Font,
            [PdfNames.Subtype] = CidFontType0,
            [BaseFont] = new PdfName(name),
            [CidSystemInfo] = SystemInfo(),
            [FontDescriptor] = WriteDescriptor(file, name, FontFile3, program),
            [PdfNames.W] = widths,
        });

        WriteType0(file, name, cidFont, shown.Where(entry => entry.Value.Text is not null).Select(entry => (entry.Key, entry.Value.Text!)).ToList());
    }

    private void WriteType0(PdfFileWriter file, string name, PdfReference cidFont, List<(ushort Code, string Text)> characters)
    {
        PdfDictionary type0 = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.Font,
            [PdfNames.Subtype] = Type0,
            [BaseFont] = new PdfName(name),
            [Encoding] = IdentityH,
            [DescendantFonts] = new PdfArray(1) { cidFont },
        };

        if (characters.Count > 0)
            type0[ToUnicode] = file.WriteStream(new PdfDictionary(), ToUnicodeMap(characters));

        file.Write(Reference, type0);
    }

    private PdfReference WriteDescriptor(PdfFileWriter file, string name, PdfName programKey, PdfReference program)
    {
        FontDescriptorInfo info = Face.Descriptor;

        PdfDictionary descriptor = new PdfDictionary
        {
            [PdfNames.Type] = FontDescriptor,
            [FontName] = new PdfName(name),
            [Flags] = (int)info.Flags,
            [FontBBox] = new PdfArray(4)
            {
                info.ToGlyphSpace(info.BoundingBox.XMin),
                info.ToGlyphSpace(info.BoundingBox.YMin),
                info.ToGlyphSpace(info.BoundingBox.XMax),
                info.ToGlyphSpace(info.BoundingBox.YMax),
            },
            [ItalicAngle] = info.ItalicAngle,
            [Ascent] = info.ToGlyphSpace(info.Ascent),
            [Descent] = info.ToGlyphSpace(info.Descent),
            [CapHeight] = info.ToGlyphSpace(info.CapHeight),
            [StemV] = info.ToGlyphSpace(info.StemV),
            [programKey] = program,
        };

        if (info.XHeight > 0)
            descriptor[XHeight] = info.ToGlyphSpace(info.XHeight);

        return file.Write(descriptor);
    }

    private double Width(ushort glyph) => Face.Descriptor.ToGlyphSpace(Face.GetAdvance(glyph));

    private static PdfDictionary SystemInfo() => new PdfDictionary
    {
        [Registry] = PdfString.FromText("Adobe"),
        [Ordering] = PdfString.FromText("Identity"),
        [Supplement] = 0,
    };

    /// <summary>
    /// The face's PostScript name, which is what <c>/BaseFont</c> must carry, reduced to the characters a
    /// PostScript name may hold. A face without one is named from its full name.
    /// </summary>
    internal static string PostScriptName(OpenTypeFont face) => PostScriptName(face.Names.PostScriptName, face.Names.FullName);

    /// <inheritdoc cref="PostScriptName(OpenTypeFont)"/>
    internal static string PostScriptName(string postScriptName, string fullName)
    {
        string source = string.IsNullOrWhiteSpace(postScriptName) ? fullName : postScriptName;
        StringBuilder name = new StringBuilder(source.Length);

        foreach (char character in source)
        {
            if (character > ' ' && character < 127 && "()<>[]{}/%".IndexOf(character) < 0)
                name.Append(character);
        }

        return name.Length > 0 ? name.ToString() : "Font";
    }

    /// <summary>A CMap from each two-byte code to the UTF-16 of the character it shows.</summary>
    /// <remarks>A value that is no Unicode scalar value maps to the replacement character, U+FFFD.</remarks>
    internal static byte[] ToUnicodeMap(IReadOnlyList<(ushort Code, int Codepoint)> characters) =>
        ToUnicodeMap(characters.Select(character => (character.Code, GlyphSubset.TextOf(character.Codepoint))).ToList());

    /// <summary>
    /// A CMap from each two-byte code to the UTF-16 of the text it shows: one character, or all of a ligature's.
    /// </summary>
    internal static byte[] ToUnicodeMap(IReadOnlyList<(ushort Code, string Text)> characters)
    {
        StringBuilder map = new StringBuilder(256 + (characters.Count * 16));
        map.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n");
        map.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
        map.Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
        map.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");

        // A bfchar block may hold at most a hundred mappings.
        for (int start = 0; start < characters.Count; start += 100)
        {
            int count = Math.Min(100, characters.Count - start);
            map.Append(count.ToString(CultureInfo.InvariantCulture)).Append(" beginbfchar\n");

            for (int index = start; index < start + count; index++)
            {
                (ushort code, string text) = characters[index];
                map.Append('<').Append(code.ToString("X4", CultureInfo.InvariantCulture)).Append("> <");

                foreach (char unit in text)
                    map.Append(((int)unit).ToString("X4", CultureInfo.InvariantCulture));

                map.Append(">\n");
            }

            map.Append("endbfchar\n");
        }

        map.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        return System.Text.Encoding.ASCII.GetBytes(map.ToString());
    }

}
