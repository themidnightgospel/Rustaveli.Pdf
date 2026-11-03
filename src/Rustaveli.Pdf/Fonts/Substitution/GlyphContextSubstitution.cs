namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup types 5 and 6, format 1: contextual rules spelled out glyph by glyph, grouped by their first input glyph.
/// </summary>
internal sealed class GlyphContextSubstitution : ContextSubstitution
{
    private readonly CoverageTable _coverage;
    private readonly int _ruleSetCount;
    private readonly bool _chained;

    public GlyphContextSubstitution(ReadOnlyMemory<byte> table, int offset, bool chained)
        : base(table, offset)
    {
        (_coverage, _ruleSetCount) = ReadCoveredArray();
        _chained = chained;
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;
        int index = _coverage.IndexOf(session.Buffer.Glyphs[position]);
        int ruleSets = Offset + CoveredArrayOffset;

        return index >= 0 &&
               TryApplyRuleSet(session, lookup, position, ruleSets, _ruleSetCount, index, _chained, out next);
    }

    protected override bool Matches(ContextPart part, ushort value, ushort glyph) => glyph == value;
}
