namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup types 5 and 6, format 3: a single contextual rule whose every glyph is a coverage table, so each position
/// accepts a whole set of glyphs.
/// </summary>
internal sealed class CoverageContextSubstitution : ContextSubstitution
{
    private readonly ContextRule _rule;
    private readonly CoverageTable? _firstInput;

    public CoverageContextSubstitution(ReadOnlyMemory<byte> table, int offset, bool chained)
        : base(table, offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        int input;

        if (chained)
        {
            int backtrackCount = BigEndian.UInt16(span, offset + 2);
            int inputAt = offset + 4 + (backtrackCount * 2);
            int inputCount = BigEndian.UInt16(span, inputAt);
            int lookaheadAt = inputAt + 2 + (inputCount * 2);
            int lookaheadCount = BigEndian.UInt16(span, lookaheadAt);
            int lookupsAt = lookaheadAt + 2 + (lookaheadCount * 2);

            input = inputAt + 2;
            _rule = new ContextRule(
                backtrackCount,
                offset + 4,
                inputCount,
                input + 2,
                lookaheadCount,
                lookaheadAt + 2,
                BigEndian.UInt16(span, lookupsAt),
                lookupsAt + 2);
        }
        else
        {
            int glyphCount = BigEndian.UInt16(span, offset + 2);
            input = offset + 6;
            _rule = new ContextRule(
                0, 0, glyphCount, input + 2, 0, 0, BigEndian.UInt16(span, offset + 4), input + (glyphCount * 2));
        }

        // A rule without input glyphs is malformed and can match nothing.
        if (_rule.InputCount > 0)
            _firstInput = new CoverageTable(table, offset + BigEndian.UInt16(span, input));
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;

        return _firstInput is not null &&
               _firstInput.IndexOf(session.Buffer.Glyphs[position]) >= 0 &&
               TryApplyRule(session, lookup, position, _rule, out next);
    }

    protected override bool Matches(ContextPart part, ushort value, ushort glyph) => Covers(value, glyph);
}
