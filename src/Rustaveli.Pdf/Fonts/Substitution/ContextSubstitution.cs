namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// What contextual subtables share — lookup types 5 and 6 in all three formats, and reverse chaining: matching a
/// rule's backtrack, input and lookahead, and applying its lookup records to the input.
/// </summary>
/// <remarks>
/// <para>
/// Every part of a rule is matched past the glyphs the lookup's flags pass over. The formats differ only in what a
/// rule's values are — glyph ids, classes or coverage tables — which <see cref="Matches"/> decides.
/// </para>
/// <para>
/// A rule's lookup records apply in the order listed, each once, at the input glyph its sequence index names. A
/// nested lookup may lengthen or shorten the sequence; the positions of the rest are brought up to date after each,
/// so the next record's index counts the glyphs as they now are. Once all have applied, the lookup continues after
/// the last input glyph.
/// </para>
/// </remarks>
internal abstract class ContextSubstitution : SubstitutionSubtable
{
    protected ContextSubstitution(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
    }

    /// <summary>Whether <paramref name="glyph"/> matches a value of the rule's <paramref name="part"/>.</summary>
    protected abstract bool Matches(ContextPart part, ushort value, ushort glyph);

    /// <summary>Whether the coverage table at an offset from the subtable's start covers the glyph.</summary>
    protected bool Covers(ushort coverage, ushort glyph) =>
        new CoverageTable(Table, Offset + coverage).IndexOf(glyph) >= 0;

    /// <summary>Tries the rules of one rule set in order, applying the first that matches.</summary>
    /// <param name="session">The session applying the table.</param>
    /// <param name="lookup">The lookup the subtable belongs to.</param>
    /// <param name="position">The first input glyph, which the subtable's coverage has matched.</param>
    /// <param name="ruleSets">Where the subtable's array of rule set offsets starts.</param>
    /// <param name="ruleSetCount">How many rule sets the array holds.</param>
    /// <param name="index">The rule set to try: the first glyph's coverage index or class.</param>
    /// <param name="chained">Whether the rules have backtrack and lookahead (type 6) or not (type 5).</param>
    /// <param name="next">Where the lookup continues after a rule applied.</param>
    protected bool TryApplyRuleSet(
        SubstitutionSession session,
        SubstitutionLookup lookup,
        int position,
        int ruleSets,
        int ruleSetCount,
        int index,
        bool chained,
        out int next)
    {
        next = position;

        if (index >= ruleSetCount)
            return false;

        ReadOnlySpan<byte> span = Table.Span;
        int ruleSet = BigEndian.UInt16(span, ruleSets + (index * 2));

        // A null offset is how a font says no rule starts with this glyph or class.
        if (ruleSet == 0)
            return false;

        ruleSet += Offset;
        int ruleCount = BigEndian.UInt16(span, ruleSet);

        for (int rule = 0; rule < ruleCount; rule++)
        {
            int at = ruleSet + BigEndian.UInt16(span, ruleSet + 2 + (rule * 2));

            if (TryApplyRule(session, lookup, position, ContextRule.Read(span, at, chained), out next))
                return true;
        }

        return false;
    }

    /// <summary>Applies the rule's lookups if it matches at <paramref name="position"/>.</summary>
    protected bool TryApplyRule(
        SubstitutionSession session, SubstitutionLookup lookup, int position, ContextRule rule, out int next)
    {
        next = position;
        List<int> positions = session.Positions();

        if (!MatchRule(session, lookup, position, rule, positions))
            return false;

        ApplyLookups(session, rule, positions);

        // With every input glyph removed, the lookup goes on where the input began, at whatever glyph now follows.
        next = positions.Count == 0 ? position : positions[positions.Count - 1] + 1;
        return true;
    }

    /// <summary>
    /// Whether the rule matches with its first input glyph at <paramref name="position"/>, recording where each
    /// input glyph is.
    /// </summary>
    protected bool MatchRule(
        SubstitutionSession session, SubstitutionLookup lookup, int position, ContextRule rule, List<int> positions)
    {
        if (!session.Spend() || rule.InputCount == 0)
            return false;

        ReadOnlySpan<byte> span = Table.Span;
        ReadOnlySpan<ushort> glyphs = session.Buffer.Glyphs;
        positions.Clear();
        positions.Add(position);
        int at = position;

        for (int input = 1; input < rule.InputCount; input++)
        {
            at = lookup.NextGlyph(session, at);

            if (at < 0 || !Matches(ContextPart.Input, Value(span, rule.Input, input - 1), glyphs[at]))
                return false;

            positions.Add(at);
        }

        for (int lookahead = 0; lookahead < rule.LookaheadCount; lookahead++)
        {
            at = lookup.NextGlyph(session, at);

            if (at < 0 || !Matches(ContextPart.Lookahead, Value(span, rule.Lookahead, lookahead), glyphs[at]))
                return false;
        }

        at = position;

        for (int backtrack = 0; backtrack < rule.BacktrackCount; backtrack++)
        {
            at = lookup.PreviousGlyph(session, at);

            if (at < 0 || !Matches(ContextPart.Backtrack, Value(span, rule.Backtrack, backtrack), glyphs[at]))
                return false;
        }

        return true;
    }

    private static ushort Value(ReadOnlySpan<byte> span, int values, int index) =>
        BigEndian.UInt16(span, values + (index * 2));

    private void ApplyLookups(SubstitutionSession session, ContextRule rule, List<int> positions)
    {
        ReadOnlySpan<byte> span = Table.Span;

        for (int record = 0; record < rule.LookupCount; record++)
        {
            int at = rule.Lookups + (record * 4);
            int sequenceIndex = BigEndian.UInt16(span, at);

            if (sequenceIndex >= positions.Count ||
                session.Table.GetLookup(BigEndian.UInt16(span, at + 2)) is not SubstitutionLookup nested ||
                !session.TryEnter())
            {
                continue;
            }

            int edits = session.EditCount;
            nested.TryApplyAt(session, positions[sequenceIndex], out _);
            session.Leave();
            session.Replay(positions, edits);
        }
    }
}
