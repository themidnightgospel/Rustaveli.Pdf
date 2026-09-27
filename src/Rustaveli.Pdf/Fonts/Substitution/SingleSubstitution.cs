namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup type 1: one glyph for another — a small capital for a lowercase letter, an oldstyle figure for a lining
/// one. Format 1 adds a constant to the glyph id; format 2 lists the substitute for each covered glyph.
/// </summary>
internal sealed class SingleSubstitution : SubstitutionSubtable
{
    private readonly CoverageTable _coverage;
    private readonly int _format;
    private readonly int _delta;
    private readonly int _count;

    public SingleSubstitution(ReadOnlyMemory<byte> table, int offset, int format)
        : base(table, offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        _coverage = new CoverageTable(table, offset + BigEndian.UInt16(span, offset + 2));
        _format = format;

        if (format == 1)
        {
            _delta = BigEndian.Int16(span, offset + 4);
            return;
        }

        _count = BigEndian.UInt16(span, offset + 4);
        _ = BigEndian.Slice(span, offset + 6L, _count * 2L);
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;
        ushort glyph = session.Buffer.Glyphs[position];
        int index = _coverage.IndexOf(glyph);

        if (index < 0)
            return false;

        // The specification adds the delta modulo 65 536, so a negative delta wraps rather than going below zero.
        int substitute = _format == 1 ? (ushort)(glyph + _delta)
            : index < _count ? BigEndian.UInt16(Table.Span, Offset + 6 + (index * 2))
            : -1;

        if (!session.TryReplace(position, substitute))
            return false;

        next = position + 1;
        return true;
    }
}
