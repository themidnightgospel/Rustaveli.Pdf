namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup types 5 and 6, format 2: contextual rules over glyph classes, grouped by the class of their first input
/// glyph. A chained subtable classifies backtrack, input and lookahead glyphs separately.
/// </summary>
/// <remarks>
/// A class definition the subtable leaves out — a null offset, which fonts write for a context they never use —
/// puts every glyph in class 0.
/// </remarks>
internal sealed class ClassContextSubstitution : ContextSubstitution
{
    private readonly CoverageTable _coverage;
    private readonly ClassDefinition? _backtrackClasses;
    private readonly ClassDefinition? _inputClasses;
    private readonly ClassDefinition? _lookaheadClasses;
    private readonly int _ruleSets;
    private readonly int _ruleSetCount;
    private readonly bool _chained;

    public ClassContextSubstitution(ReadOnlyMemory<byte> table, int offset, bool chained)
        : base(table, offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        _coverage = new CoverageTable(table, offset + BigEndian.UInt16(span, offset + 2));
        _chained = chained;

        if (chained)
        {
            _backtrackClasses = ReadClasses(span, offset + 4);
            _inputClasses = ReadClasses(span, offset + 6);
            _lookaheadClasses = ReadClasses(span, offset + 8);
            _ruleSets = offset + 12;
        }
        else
        {
            _inputClasses = ReadClasses(span, offset + 4);
            _ruleSets = offset + 8;
        }

        _ruleSetCount = BigEndian.UInt16(span, _ruleSets - 2);
        _ = BigEndian.Slice(span, _ruleSets, _ruleSetCount * 2L);
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;
        ushort glyph = session.Buffer.Glyphs[position];

        if (_coverage.IndexOf(glyph) < 0)
            return false;

        int index = ClassOf(_inputClasses, glyph);
        return TryApplyRuleSet(session, lookup, position, _ruleSets, _ruleSetCount, index, _chained, out next);
    }

    protected override bool Matches(ContextPart part, ushort value, ushort glyph)
    {
        ClassDefinition? classes = part switch
        {
            ContextPart.Backtrack => _backtrackClasses,
            ContextPart.Lookahead => _lookaheadClasses,
            _ => _inputClasses
        };

        return ClassOf(classes, glyph) == value;
    }

    private static int ClassOf(ClassDefinition? classes, ushort glyph) => classes?.ClassOf(glyph) ?? 0;

    private ClassDefinition? ReadClasses(ReadOnlySpan<byte> span, int field)
    {
        int classes = BigEndian.UInt16(span, field);
        return classes == 0 ? null : new ClassDefinition(Table, Offset + classes);
    }
}
