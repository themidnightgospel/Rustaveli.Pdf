namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// What lookup types 2, 3 and 4 share: a set for each glyph they cover — a sequence, alternates or ligatures — found
/// through the coverage and an array of offsets to the sets.
/// </summary>
internal abstract class SetSubstitution : SubstitutionSubtable
{
    private readonly CoverageTable _coverage;
    private readonly int _count;

    protected SetSubstitution(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
        (_coverage, _count) = ReadCoveredArray();
    }

    /// <summary>
    /// Where the set of <paramref name="glyph"/> starts in <see cref="SubstitutionSubtable.Table"/>; -1 when the
    /// coverage leaves the glyph out or the array stops short of it.
    /// </summary>
    protected int FindSet(ushort glyph)
    {
        int index = _coverage.IndexOf(glyph);

        if (index < 0 || index >= _count)
            return -1;

        return Offset + BigEndian.UInt16(Table.Span, Offset + CoveredArrayOffset + (index * 2));
    }
}
