using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// One face of an OpenType font — TrueType or CFF outlines, from a single-font file or a collection — with the
/// metrics layout needs and the data a PDF writer embeds.
/// </summary>
/// <remarks>
/// <para>
/// Loading reads the table directory and the three small tables every other one is sized by (<c>head</c>,
/// <c>hhea</c>, <c>maxp</c>). Everything else is parsed on first use and kept, so a font used only for measuring
/// never parses its outlines, and one found only while searching for a fallback never parses its kerning.
/// </para>
/// <para>
/// Instances are immutable apart from those lazily parsed tables, whose initialisation is race-safe without
/// locking: parsing is deterministic, so if two threads parse the same table at once, either result is correct
/// and one is kept. A font can be shared by any number of concurrently generated documents.
/// </para>
/// <para>
/// Any member may throw <see cref="FontFormatException"/> when the part of the font it reads is malformed; nothing
/// else escapes from malformed data.
/// </para>
/// </remarks>
internal sealed class OpenTypeFont
{
    private readonly ReadOnlyMemory<byte> _file;
    private readonly Lazy<Os2Table?> _os2;
    private readonly Lazy<PostTable?> _post;
    private readonly Lazy<FontNames> _names;
    private readonly Lazy<CharacterMap> _characterMap;
    private readonly Lazy<HorizontalMetricsTable> _horizontalMetrics;
    private readonly Lazy<GlyphTable?> _glyphs;
    private readonly Lazy<CompactFontTable?> _cff;
    private readonly Lazy<KerningSource?> _kerning;
    private readonly Lazy<GlyphDefinitionTable?> _glyphDefinitions;
    private readonly Lazy<GlyphSubstitutionTable?> _substitutions;
    private readonly Lazy<FaceStyle> _style;
    private readonly Lazy<LineMetrics> _lineMetrics;
    private readonly Lazy<FontDescriptorInfo> _descriptor;

    private OpenTypeFont(ReadOnlyMemory<byte> file, int faceIndex)
    {
        _file = file;
        FaceIndex = faceIndex;

        ReadOnlySpan<byte> span = file.Span;
        Tables = TableDirectory.Read(span, FontContainer.GetFaceOffset(span, faceIndex), span.Length);

        foreach (uint required in new[] { TableTag.Head, TableTag.Hhea, TableTag.Maxp, TableTag.Hmtx, TableTag.Cmap })
        {
            if (!Tables.Contains(required))
                throw new FontFormatException($"The font has no '{TableTag.ToString(required)}' table.");
        }

        Head = new HeadTable(Table(TableTag.Head).Span);
        HorizontalHeader = new HorizontalHeaderTable(Table(TableTag.Hhea).Span);
        GlyphCount = new MaximumProfileTable(Table(TableTag.Maxp).Span).NumGlyphs;
        Outlines = Tables.Outlines;

        // PublicationOnly: no lock, and a failure is not cached — every access to a malformed table reports it.
        const LazyThreadSafetyMode Mode = LazyThreadSafetyMode.PublicationOnly;
        _os2 = new Lazy<Os2Table?>(() => Optional(TableTag.Os2, static data => new Os2Table(data.Span)), Mode);
        _post = new Lazy<PostTable?>(() => Optional(TableTag.Post, static data => new PostTable(data.Span)), Mode);
        _names = new Lazy<FontNames>(ReadNames, Mode);
        _characterMap = new Lazy<CharacterMap>(() => new CharacterMap(Table(TableTag.Cmap), GlyphCount), Mode);
        _horizontalMetrics = new Lazy<HorizontalMetricsTable>(
            () => new HorizontalMetricsTable(Table(TableTag.Hmtx), HorizontalHeader.NumberOfHMetrics, GlyphCount),
            Mode);
        _glyphs = new Lazy<GlyphTable?>(ReadGlyphs, Mode);
        _cff = new Lazy<CompactFontTable?>(
            () => Optional(TableTag.Cff, static data => new CompactFontTable(data)), Mode);
        _kerning = new Lazy<KerningSource?>(ReadKerning, Mode);
        _glyphDefinitions = new Lazy<GlyphDefinitionTable?>(
            () => Refinement(TableTag.Gdef, static data => new GlyphDefinitionTable(data)), Mode);
        _substitutions = new Lazy<GlyphSubstitutionTable?>(
            () => Refinement(TableTag.Gsub, data => new GlyphSubstitutionTable(data, GlyphDefinitions, GlyphCount)),
            Mode);
        _style = new Lazy<FaceStyle>(() => FaceStyle.From(Os2, Head), Mode);
        _lineMetrics = new Lazy<LineMetrics>(() => LineMetrics.Choose(HorizontalHeader, Os2), Mode);
        _descriptor = new Lazy<FontDescriptorInfo>(() => FontDescriptorInfo.Create(this), Mode);
    }

    /// <summary>The whole file the face was loaded from, which a collection shares between its faces.</summary>
    public ReadOnlyMemory<byte> FileData => _file;

    /// <summary>The face's position in its collection; 0 for a single-font file.</summary>
    public int FaceIndex { get; }

    public TableDirectory Tables { get; }

    public OutlineFormat Outlines { get; }

    public int GlyphCount { get; }

    public int UnitsPerEm => Head.UnitsPerEm;

    public HeadTable Head { get; }

    public HorizontalHeaderTable HorizontalHeader { get; }

    /// <summary>Null when the font has no <c>OS/2</c> table, as some Macintosh fonts do not.</summary>
    public Os2Table? Os2 => _os2.Value;

    public PostTable? Post => _post.Value;

    public FontNames Names => _names.Value;

    public CharacterMap CharacterMap => _characterMap.Value;

    public HorizontalMetricsTable HorizontalMetrics => _horizontalMetrics.Value;

    /// <summary>The TrueType outlines; null for fonts with other outlines.</summary>
    public GlyphTable? Glyphs => _glyphs.Value;

    /// <summary>What the <c>CFF </c> table says of itself; null for fonts with other outlines.</summary>
    public CompactFontTable? Cff => _cff.Value;

    /// <summary>
    /// The font's pair kerning: GPOS when its <c>kern</c> feature has pair adjustments, otherwise the legacy
    /// <c>kern</c> table, as shapers choose. Null when the font kerns nothing.
    /// </summary>
    public KerningSource? Kerning => _kerning.Value;

    /// <summary>The glyph classes and mark sets of the <c>GDEF</c> table; null when the font has none.</summary>
    public GlyphDefinitionTable? GlyphDefinitions => _glyphDefinitions.Value;

    /// <summary>
    /// The font's glyph substitutions — ligatures, contextual and stylistic forms — from its <c>GSUB</c> table; null
    /// when it has none.
    /// </summary>
    public GlyphSubstitutionTable? Substitutions => _substitutions.Value;

    public FaceStyle Style => _style.Value;

    /// <inheritdoc cref="Fonts.LineMetrics"/>
    public LineMetrics LineMetrics => _lineMetrics.Value;

    public FontEmbedding Embedding => new FontEmbedding(Os2?.FsType ?? 0);

    public FontDescriptorInfo Descriptor => _descriptor.Value;

    /// <summary>Loads face <paramref name="faceIndex"/> of a font file held in memory.</summary>
    /// <remarks>The data is used in place, not copied, and must not change afterwards.</remarks>
    public static OpenTypeFont Load(ReadOnlyMemory<byte> data, int faceIndex = 0) => new OpenTypeFont(data, faceIndex);

    /// <summary>Loads face <paramref name="faceIndex"/> from a stream, which is read to its end.</summary>
    public static OpenTypeFont Load(Stream stream, int faceIndex = 0) => Load(ReadAll(stream), faceIndex);

    public static OpenTypeFont LoadFile(string path, int faceIndex = 0) => Load(File.ReadAllBytes(path), faceIndex);

    /// <summary>Loads every face of a font file: one for a single font, all of them for a collection.</summary>
    public static IReadOnlyList<OpenTypeFont> LoadAll(ReadOnlyMemory<byte> data)
    {
        int count = FontContainer.CountFaces(data.Span);

        // A collection lists an offset per face; a count the file has no room for is corrupt, and must not size
        // an array of millions of faces before the first one fails to load.
        if (FontContainer.IsCollection(data.Span))
            _ = BigEndian.Slice(data.Span, 0, FontContainer.CollectionHeaderSize + (4L * count));

        OpenTypeFont[] faces = new OpenTypeFont[count];

        for (int index = 0; index < count; index++)
            faces[index] = new OpenTypeFont(data, index);

        return faces;
    }

    /// <summary>
    /// The face as a font file of its own, for embedding it whole: a CFF font, or a TrueType font whose licence
    /// forbids subsetting. A single-font file is returned as it is; a face of a collection has its tables written
    /// out into a new file, without any digital signature, which would no longer match.
    /// </summary>
    public byte[] ToStandaloneFile()
    {
        if (!FontContainer.IsCollection(_file.Span))
            return _file.ToArray();

        const uint DigitalSignature = 0x44534947; // 'DSIG'
        List<KeyValuePair<uint, byte[]>> tables = [];

        foreach (TableRecord record in Tables.Records)
        {
            if (record.Tag != DigitalSignature)
                tables.Add(new(record.Tag, _file.Slice(record.Offset, record.Length).ToArray()));
        }

        return SfntWriter.Write(Tables.SfntVersion, tables);
    }

    /// <summary>The table's bytes, or false when the font does not have it.</summary>
    public bool TryGetTable(uint tag, out ReadOnlyMemory<byte> data)
    {
        if (Tables.TryGet(tag, out TableRecord record))
        {
            data = _file.Slice(record.Offset, record.Length);
            return true;
        }

        data = default;
        return false;
    }

    // ---- Glyphs and metrics --------------------------------------------------------------------------------------

    /// <summary>The glyph for a Unicode code point, or 0 (.notdef) when the font lacks it.</summary>
    public ushort GetGlyphId(int codepoint) => CharacterMap.GetGlyph(codepoint);

    /// <summary>True when the font has a glyph of its own for the code point.</summary>
    public bool HasGlyph(int codepoint) => CharacterMap.GetGlyph(codepoint) != 0;

    /// <summary>The glyph's advance width in font units.</summary>
    public int GetAdvance(ushort glyph)
    {
        CheckGlyph(glyph);
        return HorizontalMetrics.GetAdvance(glyph);
    }

    /// <summary>The glyph's advance width in points at <paramref name="pointSize"/>.</summary>
    public float GetAdvance(ushort glyph, float pointSize) => ToPoints(GetAdvance(glyph), pointSize);

    /// <summary>The kerning between two glyphs in font units, negative to bring them closer; 0 when none.</summary>
    public int GetKerning(ushort left, ushort right)
    {
        CheckGlyph(left);
        CheckGlyph(right);
        return Kerning?.GetAdjustment(left, right) ?? 0;
    }

    /// <summary>The kerning between two glyphs in points at <paramref name="pointSize"/>.</summary>
    public float GetKerning(ushort left, ushort right, float pointSize) =>
        ToPoints(GetKerning(left, right), pointSize);

    /// <summary>The glyph's bounding box in font units; false for a glyph without an outline, or a CFF font.</summary>
    public bool TryGetGlyphBounds(ushort glyph, out GlyphBounds bounds)
    {
        CheckGlyph(glyph);

        if (Glyphs is GlyphTable glyphs)
            return glyphs.TryGetBounds(glyph, out bounds);

        bounds = default;
        return false;
    }

    /// <summary>A length in font units, in points at <paramref name="pointSize"/>.</summary>
    public float ToPoints(long fontUnits, float pointSize) => fontUnits * pointSize / UnitsPerEm;

    // ---- Text ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// The advance of the whole text in font units: each character's glyph, plus pair kerning between neighbours
    /// when <paramref name="kerning"/> is set. A surrogate pair is one character; a lone surrogate measures as a
    /// missing glyph.
    /// </summary>
    public long MeasureWidthInUnits(ReadOnlySpan<char> text, bool kerning = true)
    {
        KerningSource? pairs = kerning ? Kerning : null;
        HorizontalMetricsTable metrics = HorizontalMetrics;
        long width = 0;
        ushort previous = 0;
        int index = 0;

        while (index < text.Length)
        {
            bool first = index == 0;
            ushort glyph = GetGlyphId(ReadCodepoint(text, ref index));

            if (!first && pairs is not null)
                width += pairs.GetAdjustment(previous, glyph);

            width += metrics.GetAdvance(glyph);
            previous = glyph;
        }

        return width;
    }

    /// <summary>
    /// The width of the text in points, set on one line at <paramref name="pointSize"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="pointSize">The type size.</param>
    /// <param name="tracking">
    /// Extra space between characters, in points. Applied between characters only — N characters have N - 1
    /// gaps — so centred text stays centred.
    /// </param>
    /// <param name="kerning">Whether to apply the font's pair kerning.</param>
    public float MeasureWidth(ReadOnlySpan<char> text, float pointSize, float tracking = 0f, bool kerning = true)
    {
        if (text.IsEmpty)
            return 0f;

        return ToPoints(MeasureWidthInUnits(text, kerning), pointSize) + (tracking * (CountCharacters(text) - 1));
    }

    /// <summary>
    /// How many UTF-16 code units from the start of the text fit within <paramref name="maxWidth"/> points — never
    /// ending inside a surrogate pair. 0 when not even the first character fits.
    /// </summary>
    /// <remarks>
    /// A prefix fits exactly when <see cref="MeasureWidth"/> of that prefix is at most the width: both use the same
    /// arithmetic, so a width measured by one is always accepted by the other.
    /// </remarks>
    public int CountFitting(
        ReadOnlySpan<char> text, float pointSize, float maxWidth, float tracking = 0f, bool kerning = true)
    {
        if (text.IsEmpty || maxWidth <= 0)
            return 0;

        KerningSource? pairs = kerning ? Kerning : null;
        HorizontalMetricsTable metrics = HorizontalMetrics;
        long units = 0;
        int gaps = 0;
        ushort previous = 0;
        int index = 0;

        while (index < text.Length)
        {
            int start = index;
            ushort glyph = GetGlyphId(ReadCodepoint(text, ref index));
            long next = units + metrics.GetAdvance(glyph);

            if (start > 0 && pairs is not null)
                next += pairs.GetAdjustment(previous, glyph);

            if (ToPoints(next, pointSize) + (tracking * gaps) > maxWidth)
                return start;

            units = next;
            previous = glyph;
            gaps++;
        }

        return text.Length;
    }

    private static int ReadCodepoint(ReadOnlySpan<char> text, ref int index)
    {
        char character = text[index];

        if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            int codepoint = char.ConvertToUtf32(character, text[index + 1]);
            index += 2;
            return codepoint;
        }

        index++;
        return character;
    }

    private static int CountCharacters(ReadOnlySpan<char> text)
    {
        int count = 0;

        for (int index = 0; index < text.Length; count++)
            ReadCodepoint(text, ref index);

        return count;
    }

    private void CheckGlyph(ushort glyph)
    {
        if (glyph >= GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyph), glyph, $"The font has {GlyphCount} glyphs.");
    }

    // ---- Lazily read tables --------------------------------------------------------------------------------------

    private ReadOnlyMemory<byte> Table(uint tag) =>
        TryGetTable(tag, out ReadOnlyMemory<byte> data)
            ? data
            : throw new FontFormatException($"The font has no '{TableTag.ToString(tag)}' table.");

    private T? Optional<T>(uint tag, Func<ReadOnlyMemory<byte>, T> read)
        where T : class =>
        TryGetTable(tag, out ReadOnlyMemory<byte> data) ? read(data) : null;

    /// <summary>
    /// A table that refines how text is set rather than what it shows, such as substitutions: one the face gets wrong
    /// is left out, as shapers leave it, so the text is still set, only without what the table would have added.
    /// </summary>
    private T? Refinement<T>(uint tag, Func<ReadOnlyMemory<byte>, T> read)
        where T : class
    {
        try
        {
            return Optional(tag, read);
        }
        catch (FontFormatException)
        {
            return null;
        }
    }

    private FontNames ReadNames() =>
        Optional(TableTag.Name, static data => NameTable.Read(data.Span)) ?? FontNames.None;

    private GlyphTable? ReadGlyphs() => Outlines == OutlineFormat.TrueType
        ? new GlyphTable(Table(TableTag.Glyf), Table(TableTag.Loca), GlyphCount, Head.IndexToLocFormat)
        : null;

    /// <summary>
    /// Kerning refines how text is set, so either table read wrongly is left out like any other refinement: a GPOS
    /// table that cannot be read gives way to the <c>kern</c> table, as it would were it absent.
    /// </summary>
    private KerningSource? ReadKerning()
    {
        if (Refinement(TableTag.Gpos, static data => new GlyphPositioningKerning(data)) is { HasPairs: true } positioning)
            return positioning;

        if (Refinement(TableTag.Kern, static data => new LegacyKerningTable(data)) is { HasPairs: true } legacy)
            return legacy;

        return null;
    }

    private static ReadOnlyMemory<byte> ReadAll(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Sized up front when the stream knows its length, so a large font is not copied through doubling buffers.
        long remaining = stream.CanSeek ? stream.Length - stream.Position : 0;
        using MemoryStream copy = new MemoryStream(remaining is > 0 and <= int.MaxValue ? (int)remaining : 0);
        stream.CopyTo(copy);

        return copy.GetBuffer().AsMemory(0, (int)copy.Length);
    }
}
