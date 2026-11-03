namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup type 2: one glyph for several, such as a precomposed letter for its base letter and accent. The glyphs
/// replacing one share its cluster, so together they stand for its characters.
/// </summary>
internal sealed class MultipleSubstitution : SetSubstitution
{
    public MultipleSubstitution(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;
        int sequence = FindSet(session.Buffer.Glyphs[position]);

        if (sequence < 0)
            return false;

        ReadOnlySpan<byte> span = Table.Span;
        int count = BigEndian.UInt16(span, sequence);

        // Deleting the glyph brings the next one to this position, which is where the lookup goes on.
        if (count == 0)
        {
            Delete(session, position);
            return true;
        }

        for (int glyph = 0; glyph < count; glyph++)
        {
            if (!session.IsGlyph(BigEndian.UInt16(span, sequence + 2 + (glyph * 2))))
                return false;
        }

        if (!session.TryInsertAfter(position, count - 1))
            return false;

        for (int glyph = 0; glyph < count; glyph++)
            session.Buffer.Replace(position + glyph, BigEndian.UInt16(span, sequence + 2 + (glyph * 2)));

        next = position + count;
        return true;
    }

    /// <summary>
    /// Removes the glyph, as an empty sequence asks. The specification forbids empty sequences, but fonts use them
    /// and shapers honour them; the glyph's characters join the cluster before it, or after it at the start, so the
    /// text they stand for is not lost.
    /// </summary>
    private static void Delete(SubstitutionSession session, int position)
    {
        if (session.Buffer.Count > 1)
        {
            int first = Math.Max(position - 1, 0);
            session.Buffer.MergeClusters(first, first + 1);
        }

        session.Remove(position);
    }
}
