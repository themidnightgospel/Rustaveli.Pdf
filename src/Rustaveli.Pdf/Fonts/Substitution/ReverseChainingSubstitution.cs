namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup type 8: one glyph for another where coverage tables before and after it match, applied from the end of
/// the text back — so the choice at one glyph can depend on the choice already made for the glyph after it, as
/// Nastaliq fonts choose each letter's form from the letters that follow.
/// </summary>
internal sealed class ReverseChainingSubstitution : ContextSubstitution
{
    private readonly CoverageTable _coverage;
    private readonly ContextRule _context;
    private readonly int _substitutes;
    private readonly int _substituteCount;

    public ReverseChainingSubstitution(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        _coverage = new CoverageTable(table, offset + BigEndian.UInt16(span, offset + 2));

        int backtrackCount = BigEndian.UInt16(span, offset + 4);
        int lookaheadAt = offset + 6 + (backtrackCount * 2);
        int lookaheadCount = BigEndian.UInt16(span, lookaheadAt);
        int substitutesAt = lookaheadAt + 2 + (lookaheadCount * 2);

        _context = new ContextRule(backtrackCount, offset + 6, 1, 0, lookaheadCount, lookaheadAt + 2, 0, 0);
        _substituteCount = BigEndian.UInt16(span, substitutesAt);
        _substitutes = substitutesAt + 2;
        _ = BigEndian.Slice(span, _substitutes, _substituteCount * 2L);
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;
        int index = _coverage.IndexOf(session.Buffer.Glyphs[position]);

        if (index < 0 || !MatchRule(session, lookup, position, _context, session.Positions()))
            return false;

        int substitute = index < _substituteCount ? BigEndian.UInt16(Table.Span, _substitutes + (index * 2)) : -1;

        if (!session.TryReplace(position, substitute))
            return false;

        next = position + 1;
        return true;
    }

    protected override bool Matches(ContextPart part, ushort value, ushort glyph) => Covers(value, glyph);
}
