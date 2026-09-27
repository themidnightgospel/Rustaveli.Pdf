namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup type 3: one glyph for one of several alternates — a swash, a stylistic variant. The value of the feature
/// being applied chooses: 1 the first alternate, 2 the second, and a value past the last none at all.
/// </summary>
internal sealed class AlternateSubstitution : SubstitutionSubtable
{
    private readonly CoverageTable _coverage;
    private readonly int _count;

    public AlternateSubstitution(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        _coverage = new CoverageTable(table, offset + BigEndian.UInt16(span, offset + 2));
        _count = BigEndian.UInt16(span, offset + 4);
        _ = BigEndian.Slice(span, offset + 6L, _count * 2L);
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;
        int index = _coverage.IndexOf(session.Buffer.Glyphs[position]);

        if (index < 0 || index >= _count)
            return false;

        ReadOnlySpan<byte> span = Table.Span;
        int set = Offset + BigEndian.UInt16(span, Offset + 6 + (index * 2));
        int choice = session.FeatureValue - 1;

        if (choice < 0 || choice >= BigEndian.UInt16(span, set))
            return false;

        if (!session.TryReplace(position, BigEndian.UInt16(span, set + 2 + (choice * 2))))
            return false;

        next = position + 1;
        return true;
    }
}
