namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// One subtable of a GSUB lookup, which substitutes at a glyph when its coverage and context match there.
/// </summary>
/// <remarks>
/// Subtables read their arrays from the font data as they need them, with every read checked, rather than copying
/// them out when parsed: a lookup costs nothing until a glyph it covers turns up. Structural damage — a format that
/// does not exist, data that ends early — throws <see cref="FontFormatException"/>. An index the data could hold
/// but that points nowhere — a coverage index past an array, a lookup or sequence index past its list, a glyph the
/// font lacks — makes that one substitution not apply, as a shaper would treat it.
/// </remarks>
internal abstract class SubstitutionSubtable
{
    protected SubstitutionSubtable(ReadOnlyMemory<byte> table, int offset)
    {
        Table = table;
        Offset = offset;
    }

    /// <summary>The whole GSUB table, which every offset is resolved against.</summary>
    protected ReadOnlyMemory<byte> Table { get; }

    /// <summary>Where the subtable starts in <see cref="Table"/>.</summary>
    protected int Offset { get; }

    /// <summary>
    /// Substitutes at <paramref name="position"/> if the subtable applies there; false, changing nothing, if not.
    /// </summary>
    /// <param name="session">The session applying the table, whose buffer is edited.</param>
    /// <param name="lookup">The subtable's lookup, whose flags decide which glyphs a match passes over.</param>
    /// <param name="position">The glyph to substitute at.</param>
    /// <param name="next">
    /// After a substitution, where the lookup continues: the glyph after those consumed. Without one, the position.
    /// </param>
    public abstract bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next);

    /// <summary>Reads the subtable of lookup type <paramref name="type"/> at <paramref name="offset"/>.</summary>
    public static SubstitutionSubtable Read(ReadOnlyMemory<byte> table, int type, int offset)
    {
        int format = BigEndian.UInt16(table.Span, offset);

        return (type, format) switch
        {
            (1, 1 or 2) => new SingleSubstitution(table, offset, format),
            (2, 1) => new MultipleSubstitution(table, offset),
            (3, 1) => new AlternateSubstitution(table, offset),
            (4, 1) => new LigatureSubstitution(table, offset),
            (5 or 6, 1) => new GlyphContextSubstitution(table, offset, chained: type == 6),
            (5 or 6, 2) => new ClassContextSubstitution(table, offset, chained: type == 6),
            (5 or 6, 3) => new CoverageContextSubstitution(table, offset, chained: type == 6),
            (8, 1) => new ReverseChainingSubstitution(table, offset),
            _ => throw new FontFormatException($"GSUB lookup type {type} has no subtable format {format}.")
        };
    }
}
