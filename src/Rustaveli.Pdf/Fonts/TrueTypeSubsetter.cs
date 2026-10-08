namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Cuts a TrueType font down to the glyphs a document uses, so a PDF embeds kilobytes of outlines instead of the
/// whole font.
/// </summary>
/// <remarks>
/// <para>
/// The subset keeps glyph 0 (.notdef), the glyphs asked for, and every glyph those reference as components of a
/// composite glyph, transitively: an "Å" drawn as "A" plus a ring needs both. Glyphs are renumbered compactly, and
/// the component references inside composite glyphs are rewritten to the new numbers.
/// </para>
/// <para>
/// The result holds the tables a PDF viewer needs to draw TrueType outlines — <c>head</c>, <c>hhea</c>,
/// <c>maxp</c>, <c>hmtx</c>, <c>loca</c>, <c>glyf</c> — plus a <c>cmap</c> for the kept characters and a version 3
/// <c>post</c>, which validators and font tools expect. Names, layout tables, kerning, signatures and everything else
/// are dropped: none of them are read from an embedded CIDFont, and the names alone can outweigh a small subset.
/// </para>
/// <para>
/// Hinting is taken out unless asked to be kept — the programs <c>cvt </c>, <c>fpgm</c> and <c>prep</c>, and every
/// glyph's instructions — save in the fonts that need it to be read at all (<see cref="GlyphHinting"/>).
/// </para>
/// <para>
/// Only TrueType outlines are subset here; CFF fonts are subset by <see cref="CffSubsetter"/>.
/// </para>
/// </remarks>
internal static class TrueTypeSubsetter
{
    /// <summary>
    /// The largest glyph data a short <c>loca</c> can address: it stores offsets halved in 16 bits.
    /// </summary>
    private const int ShortLocaLimit = 0xFFFF * 2;

    private static readonly uint[] HintingTables = [TableTag.Cvt, TableTag.Fpgm, TableTag.Prep];

    /// <summary>
    /// A subset of <paramref name="glyphs"/> and the glyphs they need, numbered in their original order.
    /// </summary>
    public static TrueTypeSubset Subset(OpenTypeFont font, IEnumerable<ushort> glyphs, bool keepHinting = false)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);

        List<ushort> order = [];
        Dictionary<ushort, ushort> numbers = new Dictionary<ushort, ushort>();
        Number(order, numbers, 0);

        foreach (ushort glyph in glyphs)
            Number(order, numbers, glyph);

        AddComponents(font, order, numbers);
        order.Sort();

        // Numbered again in their original order, now that they are sorted into it.
        for (int index = 0; index < order.Count; index++)
            numbers[order[index]] = (ushort)index;

        return Build(font, order, numbers, keepHinting);
    }

    /// <summary>
    /// A subset whose first glyphs are numbered exactly as listed — the numbering a document has already written
    /// into its content streams — followed by any glyphs they need as components.
    /// </summary>
    /// <param name="font">The font to subset.</param>
    /// <param name="numbering">Original glyph ids in subset order; the first must be 0, and none may repeat.</param>
    /// <param name="keepHinting">Whether the glyphs keep their hinting, which fonts that need it keep regardless.</param>
    public static TrueTypeSubset SubsetInOrder(OpenTypeFont font, IReadOnlyList<ushort> numbering, bool keepHinting = false)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(numbering);

        if (numbering.Count == 0 || numbering[0] != 0)
            throw new ArgumentException("A subset numbers .notdef as glyph 0.", nameof(numbering));

        // The whole numbering is checked before any of its glyphs is looked up in the font.
        List<ushort> order = new List<ushort>(numbering.Count);
        Dictionary<ushort, ushort> numbers = new Dictionary<ushort, ushort>(numbering.Count);

        for (int index = 0; index < numbering.Count; index++)
        {
            if (!Number(order, numbers, numbering[index]))
                throw new ArgumentException("A glyph can have only one number in a subset.", nameof(numbering));
        }

        AddComponents(font, order, numbers);
        return Build(font, order, numbers, keepHinting);
    }

    /// <summary>
    /// Checks that every glyph listed is in the font, then appends the components they reference, transitively, each
    /// once and numbered by its place.
    /// </summary>
    private static void AddComponents(OpenTypeFont font, List<ushort> glyphs, Dictionary<ushort, ushort> numbers)
    {
        GlyphTable table = RequireTrueType(font);
        List<GlyphComponent> components = new List<GlyphComponent>();

        foreach (ushort glyph in glyphs)
        {
            if (glyph >= font.GlyphCount)
                throw new ArgumentOutOfRangeException(nameof(glyphs), glyph, $"The font has {font.GlyphCount} glyphs.");
        }

        // Breadth-first over the growing list; the numbers make each glyph enter it once, which also ends a cycle of
        // composites that reference one another.
        for (int index = 0; index < glyphs.Count; index++)
        {
            components.Clear();
            table.AddComponents(glyphs[index], components);

            foreach (GlyphComponent component in components)
            {
                if (component.GlyphId >= font.GlyphCount)
                {
                    throw new FontFormatException(
                        $"Glyph {glyphs[index]} uses glyph {component.GlyphId}, which does not exist.");
                }

                Number(glyphs, numbers, component.GlyphId);
            }
        }
    }

    /// <summary>
    /// Numbers a glyph next, by its place in <paramref name="order"/>; false if it has a number already.
    /// </summary>
    private static bool Number(List<ushort> order, Dictionary<ushort, ushort> numbers, ushort glyph)
    {
        if (!numbers.TryAdd(glyph, (ushort)order.Count))
            return false;

        order.Add(glyph);
        return true;
    }

    /// <summary>
    /// The subset of the glyphs in <paramref name="order"/>, each numbered in <paramref name="numbers"/> by its place
    /// there.
    /// </summary>
    private static TrueTypeSubset Build(
        OpenTypeFont font, List<ushort> order, Dictionary<ushort, ushort> numbers, bool keepHinting)
    {
        GlyphTable glyphs = RequireTrueType(font);
        bool hinted = keepHinting || GlyphHinting.IsNeededBy(font.Names);
        OutlineData outlines = WriteOutlines(font, glyphs, order, numbers, hinted);
        bool longLoca = outlines.Glyf.Length > ShortLocaLimit;

        List<KeyValuePair<uint, byte[]>> tables =
        [
            new(TableTag.Glyf, outlines.Glyf),
            new(TableTag.Loca, WriteLoca(outlines.Offsets, longLoca)),
            new(TableTag.Head, WriteHead(font, outlines, longLoca)),
            new(TableTag.Hhea, WriteHorizontalHeader(font, outlines)),
            new(TableTag.Hmtx, WriteHorizontalMetrics(outlines)),
            new(TableTag.Maxp, WriteMaximumProfile(font, order.Count)),
            new(TableTag.Post, WritePost(font)),
            new(TableTag.Cmap, WriteCharacterMap(font, numbers))
        ];

        foreach (uint tag in HintingTables)
        {
            if (hinted && font.TryGetTable(tag, out ReadOnlyMemory<byte> data))
                tables.Add(new(tag, data.ToArray()));
        }

        byte[] file = SfntWriter.Write(TableDirectory.TrueTypeVersion, tables);
        ushort[] originals = order.ToArray();

        return new TrueTypeSubset(file, originals, numbers, SubsetTag(font.Names.PostScriptName, originals));
    }

    private static GlyphTable RequireTrueType(OpenTypeFont font) =>
        font.Glyphs ?? throw new NotSupportedException(
            $"Only TrueType outlines are subset; a font with {font.Outlines} outlines is embedded whole.");

    private static OutlineData WriteOutlines(
        OpenTypeFont font, GlyphTable glyphs, List<ushort> order, Dictionary<ushort, ushort> numbers, bool hinted)
    {
        FontDataWriter glyf = new FontDataWriter();
        OutlineData result = new OutlineData(order.Count);
        List<GlyphComponent> components = new List<GlyphComponent>();

        for (int index = 0; index < order.Count; index++)
        {
            ushort original = order[index];
            ReadOnlySpan<byte> data = glyphs.GetGlyphData(original).Span;
            result.Offsets[index] = glyf.Length;
            result.Advances[index] = font.HorizontalMetrics.GetAdvance(original);
            result.Bearings[index] = font.HorizontalMetrics.GetLeftSideBearing(original);

            if (data.IsEmpty)
                continue;

            if (!hinted)
                data = GlyphHinting.Strip(data);

            int start = glyf.Length;
            glyf.Bytes(data);

            if (GlyphTable.IsComposite(data))
            {
                components.Clear();
                CompositeGlyph.ReadComponents(data, components);

                foreach (GlyphComponent component in components)
                    glyf.PatchUInt16(start + component.GlyphIdOffset, numbers[component.GlyphId]);
            }

            // Four-byte alignment keeps every offset even, as a short loca requires, and aligned for readers.
            glyf.Align(4);
            glyphs.TryGetBounds(original, out GlyphBounds bounds);
            result.Include(index, bounds);
        }

        result.Offsets[order.Count] = glyf.Length;
        result.Glyf = glyf.ToArray();
        return result;
    }

    private static byte[] WriteLoca(int[] offsets, bool longFormat)
    {
        FontDataWriter loca = new FontDataWriter(offsets.Length * 4);

        foreach (int offset in offsets)
        {
            if (longFormat)
                loca.UInt32((uint)offset);
            else
                loca.UInt16(offset / 2);
        }

        return loca.ToArray();
    }

    private static byte[] WriteHead(OpenTypeFont font, OutlineData outlines, bool longLoca)
    {
        byte[] head = CopyTable(font, TableTag.Head);
        GlyphBounds box = outlines.Bounds;

        BigEndian.WriteUInt16(head, HeadTable.BoundingBoxOffset, (ushort)box.XMin);
        BigEndian.WriteUInt16(head, HeadTable.BoundingBoxOffset + 2, (ushort)box.YMin);
        BigEndian.WriteUInt16(head, HeadTable.BoundingBoxOffset + 4, (ushort)box.XMax);
        BigEndian.WriteUInt16(head, HeadTable.BoundingBoxOffset + 6, (ushort)box.YMax);
        BigEndian.WriteUInt16(head, HeadTable.IndexToLocFormatOffset, longLoca ? (ushort)1 : (ushort)0);

        return head;
    }

    /// <summary>
    /// The original <c>hhea</c> with its glyph count and its extremes recomputed over the glyphs kept, which
    /// validators check against the outlines.
    /// </summary>
    private static byte[] WriteHorizontalHeader(OpenTypeFont font, OutlineData outlines)
    {
        byte[] hhea = CopyTable(font, TableTag.Hhea);
        int minLeft = 0;
        int minRight = 0;
        int maxExtent = 0;
        bool any = false;

        for (int index = 0; index < outlines.Advances.Length; index++)
        {
            if (outlines.GlyphBounds[index] is not GlyphBounds bounds)
                continue;

            int left = outlines.Bearings[index];
            int extent = left + (bounds.XMax - bounds.XMin);
            int right = outlines.Advances[index] - extent;

            minLeft = any ? Math.Min(minLeft, left) : left;
            minRight = any ? Math.Min(minRight, right) : right;
            maxExtent = any ? Math.Max(maxExtent, extent) : extent;
            any = true;
        }

        BigEndian.WriteUInt16(hhea, HorizontalHeaderTable.AdvanceWidthMaxOffset, outlines.Advances.Max());
        BigEndian.WriteUInt16(hhea, HorizontalHeaderTable.AdvanceWidthMaxOffset + 2, (ushort)minLeft);
        BigEndian.WriteUInt16(hhea, HorizontalHeaderTable.AdvanceWidthMaxOffset + 4, (ushort)minRight);
        BigEndian.WriteUInt16(hhea, HorizontalHeaderTable.AdvanceWidthMaxOffset + 6, (ushort)maxExtent);
        BigEndian.WriteUInt16(hhea, HorizontalHeaderTable.NumberOfHMetricsOffset, (ushort)outlines.MetricCount);

        return hhea;
    }

    private static byte[] WriteHorizontalMetrics(OutlineData outlines)
    {
        int count = outlines.Advances.Length;
        FontDataWriter hmtx = new FontDataWriter((count * 4) + 4);

        for (int index = 0; index < count; index++)
        {
            if (index < outlines.MetricCount)
                hmtx.UInt16(outlines.Advances[index]);

            hmtx.Int16(outlines.Bearings[index]);
        }

        return hmtx.ToArray();
    }

    private static byte[] WriteMaximumProfile(OpenTypeFont font, int glyphCount)
    {
        byte[] maxp = CopyTable(font, TableTag.Maxp);

        // The remaining fields of a version 1.0 table are maxima over all glyphs, which a subset can only lower,
        // so the original values stay valid bounds.
        BigEndian.WriteUInt16(maxp, MaximumProfileTable.NumGlyphsOffset, (ushort)glyphCount);
        return maxp;
    }

    /// <summary>A version 3 <c>post</c>: the original header without the glyph names nothing in a PDF reads.</summary>
    private static byte[] WritePost(OpenTypeFont font)
    {
        byte[] post = new byte[PostTable.HeaderSize];

        if (font.TryGetTable(TableTag.Post, out ReadOnlyMemory<byte> original) && original.Length >= 16)
            original.Span.Slice(4, 12).CopyTo(post.AsSpan(4));

        BigEndian.WriteUInt32(post, 0, 0x00030000);
        return post;
    }

    private static byte[] WriteCharacterMap(OpenTypeFont font, Dictionary<ushort, ushort> numbers)
    {
        List<KeyValuePair<int, ushort>> kept = [];

        foreach (KeyValuePair<int, ushort> mapping in font.CharacterMap.EnumerateMappings())
        {
            if (numbers.TryGetValue(mapping.Value, out ushort glyph) && glyph != 0)
                kept.Add(new KeyValuePair<int, ushort>(mapping.Key, glyph));
        }

        return CharacterMapWriter.Write(kept, font.CharacterMap.Encoding == CharacterEncoding.Symbol);
    }

    /// <summary>
    /// A copy of one of the tables every loaded font has, to patch: loading already checked that each is present and
    /// long enough for the fields rewritten here.
    /// </summary>
    private static byte[] CopyTable(OpenTypeFont font, uint tag)
    {
        font.TryGetTable(tag, out ReadOnlyMemory<byte> data);
        return data.ToArray();
    }

    /// <summary>
    /// Six letters from a hash of the font name and the kept glyphs, as a PDF prefixes a subset font's name. FNV-1a
    /// rather than <see cref="string.GetHashCode()"/>, which is randomised per process.
    /// </summary>
    internal static string SubsetTag(string fontName, IEnumerable<ushort> glyphs)
    {
        uint hash = 2166136261;

        foreach (char character in fontName)
            hash = (hash ^ character) * 16777619;

        foreach (ushort glyph in glyphs)
        {
            hash = (hash ^ (uint)(glyph & 0xFF)) * 16777619;
            hash = (hash ^ (uint)(glyph >> 8)) * 16777619;
        }

        char[] tag = new char[6];

        for (int index = 0; index < tag.Length; index++)
        {
            tag[index] = (char)('A' + (hash % 26));
            hash /= 26;
        }

        return new string(tag);
    }

    /// <summary>The rewritten outlines and the per-glyph metrics gathered while writing them.</summary>
    private sealed class OutlineData(int glyphCount)
    {
        private bool _hasBounds;

        public byte[] Glyf { get; set; } = [];

        /// <summary>Each glyph's offset in <see cref="Glyf"/>, plus the end of the last.</summary>
        public int[] Offsets { get; } = new int[glyphCount + 1];

        public ushort[] Advances { get; } = new ushort[glyphCount];

        public short[] Bearings { get; } = new short[glyphCount];

        /// <summary>Each glyph's bounding box; null for a glyph without an outline.</summary>
        public GlyphBounds?[] GlyphBounds { get; } = new GlyphBounds?[glyphCount];

        /// <summary>The union of the glyphs' bounding boxes; all zero when no glyph has an outline.</summary>
        public GlyphBounds Bounds { get; private set; }

        /// <summary>
        /// How many glyphs need an advance of their own in <c>hmtx</c>: trailing glyphs sharing the last advance
        /// store only a bearing.
        /// </summary>
        public int MetricCount
        {
            get
            {
                int count = Advances.Length;

                while (count > 1 && Advances[count - 1] == Advances[count - 2])
                    count--;

                return count;
            }
        }

        public void Include(int glyph, GlyphBounds bounds)
        {
            GlyphBounds[glyph] = bounds;
            Bounds = !_hasBounds
                ? bounds
                : new GlyphBounds(
                    Math.Min(Bounds.XMin, bounds.XMin),
                    Math.Min(Bounds.YMin, bounds.YMin),
                    Math.Max(Bounds.XMax, bounds.XMax),
                    Math.Max(Bounds.YMax, bounds.YMax));
            _hasBounds = true;
        }
    }
}
