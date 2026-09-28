namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The GDEF table's glyph classes, mark attachment classes and mark glyph sets: what a GSUB or GPOS lookup's flags
/// consult to decide which glyphs it passes over.
/// </summary>
/// <remarks>
/// Attachment points and ligature carets serve editing and cursor placement, and the item variation store variable
/// fonts; none of them is read. A font without a glyph class definition classifies nothing, so no lookup flag passes
/// over any of its glyphs — the specification's reading, rather than guessing classes from the lookups.
/// </remarks>
internal sealed class GlyphDefinitionTable
{
    private readonly ReadOnlyMemory<byte> _table;
    private readonly ClassDefinition? _glyphClasses;
    private readonly ClassDefinition? _markAttachmentClasses;
    private readonly int _markGlyphSets;

    public GlyphDefinitionTable(ReadOnlyMemory<byte> table)
    {
        ReadOnlySpan<byte> span = table.Span;
        _table = table;

        int major = BigEndian.UInt16(span, 0);
        int minor = BigEndian.UInt16(span, 2);

        if (major != 1)
            throw new FontFormatException($"GDEF version {major}.{minor} does not exist.");

        _glyphClasses = ReadClasses(table, BigEndian.UInt16(span, 4));
        _markAttachmentClasses = ReadClasses(table, BigEndian.UInt16(span, 10));

        // Mark glyph sets arrived with version 1.2; before it the field is not there to read.
        int sets = minor >= 2 ? BigEndian.UInt16(span, 12) : 0;

        if (sets != 0)
        {
            int format = BigEndian.UInt16(span, sets);

            if (format != 1)
                throw new FontFormatException($"Mark glyph sets format {format} does not exist.");

            MarkGlyphSetCount = BigEndian.UInt16(span, sets + 2);
            _ = BigEndian.Slice(span, sets + 4L, MarkGlyphSetCount * 4L);
            _markGlyphSets = sets;
        }
    }

    public int MarkGlyphSetCount { get; }

    public GlyphClass ClassOf(ushort glyph) => (GlyphClass)(_glyphClasses?.ClassOf(glyph) ?? 0);

    /// <summary>The glyph's mark attachment class; 0 when it has none.</summary>
    public int MarkAttachmentClassOf(ushort glyph) => _markAttachmentClasses?.ClassOf(glyph) ?? 0;

    /// <summary>The marks of set <paramref name="index"/>; null when the table has no such set.</summary>
    public CoverageTable? GetMarkGlyphSet(int index)
    {
        if (index >= MarkGlyphSetCount)
            return null;

        long coverage = _markGlyphSets + (long)BigEndian.UInt32(_table.Span, _markGlyphSets + 4 + (index * 4));

        // Clamped rather than cast, so an offset past the table fails the read that follows instead of wrapping.
        return new CoverageTable(_table, (int)Math.Min(coverage, int.MaxValue));
    }

    /// <summary>
    /// True when a lookup with <paramref name="flags"/> passes over the glyph. A lookup filtering marks by a set it
    /// cannot find — <paramref name="markFilteringSet"/> null — passes over every mark.
    /// </summary>
    public bool Skips(ushort glyph, LookupFlags flags, CoverageTable? markFilteringSet)
    {
        switch (ClassOf(glyph))
        {
            case GlyphClass.Base:
                return (flags & LookupFlags.IgnoreBaseGlyphs) != 0;

            case GlyphClass.Ligature:
                return (flags & LookupFlags.IgnoreLigatures) != 0;

            case GlyphClass.Mark:
                if ((flags & LookupFlags.IgnoreMarks) != 0)
                    return true;

                // A filtering set decides alone; the attachment type then plays no part.
                if ((flags & LookupFlags.UseMarkFilteringSet) != 0)
                    return markFilteringSet is not { } set || set.IndexOf(glyph) < 0;

                int attachmentType = (int)(flags & LookupFlags.MarkAttachmentType) >> 8;
                return attachmentType != 0 && MarkAttachmentClassOf(glyph) != attachmentType;

            default:
                return false;
        }
    }

    private static ClassDefinition? ReadClasses(ReadOnlyMemory<byte> table, int offset) =>
        offset == 0 ? null : new ClassDefinition(table, offset);
}
