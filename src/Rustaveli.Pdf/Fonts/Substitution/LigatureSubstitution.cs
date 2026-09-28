namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup type 4: several glyphs for one, such as f and i for the fi ligature. The ligature takes the first
/// component's place and the merged cluster of them all, so it stands for every character it replaces.
/// </summary>
/// <remarks>
/// Components are matched past the glyphs the lookup's flags pass over; those glyphs — typically marks — stay, in
/// order, after the ligature, and join its cluster. A covered glyph's ligatures are tried in the order the font
/// lists them, which is its order of preference, and the first that matches is formed.
/// </remarks>
internal sealed class LigatureSubstitution : SubstitutionSubtable
{
    private readonly CoverageTable _coverage;
    private readonly int _count;

    public LigatureSubstitution(ReadOnlyMemory<byte> table, int offset)
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
        int ligatureCount = BigEndian.UInt16(span, set);
        List<int> components = session.Positions();

        for (int candidate = 0; candidate < ligatureCount && session.Spend(); candidate++)
        {
            int ligature = set + BigEndian.UInt16(span, set + 2 + (candidate * 2));
            ushort glyph = BigEndian.UInt16(span, ligature);

            if (Matches(session, lookup, span, ligature, position, components) && session.IsGlyph(glyph))
            {
                Form(session, components, glyph);
                next = position + 1;
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the ligature's components follow the glyph, recording where each one is.</summary>
    private static bool Matches(
        SubstitutionSession session,
        SubstitutionLookup lookup,
        ReadOnlySpan<byte> span,
        int ligature,
        int position,
        List<int> components)
    {
        ReadOnlySpan<ushort> glyphs = session.Buffer.Glyphs;
        int componentCount = BigEndian.UInt16(span, ligature + 2);
        int at = position;
        components.Clear();
        components.Add(position);

        // The count includes the first component, which the coverage has matched already.
        for (int component = 1; component < componentCount; component++)
        {
            at = lookup.NextGlyph(session, at);

            if (at < 0 || glyphs[at] != BigEndian.UInt16(span, ligature + 2 + (component * 2)))
                return false;

            components.Add(at);
        }

        return true;
    }

    private static void Form(SubstitutionSession session, List<int> components, ushort ligature)
    {
        int first = components[0];
        session.Buffer.MergeClusters(first, components[components.Count - 1]);
        session.Buffer.Replace(first, ligature);

        // From the last, so removing one does not move the others still to be removed.
        for (int component = components.Count - 1; component > 0; component--)
            session.Remove(components[component]);
    }
}
