namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Lookup type 3: one glyph for one of several alternates — a swash, a stylistic variant. The value of the feature
/// being applied chooses: 1 the first alternate, 2 the second, and a value past the last none at all.
/// </summary>
internal sealed class AlternateSubstitution : SetSubstitution
{
    public AlternateSubstitution(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
    }

    public override bool TryApply(SubstitutionSession session, SubstitutionLookup lookup, int position, out int next)
    {
        next = position;
        int set = FindSet(session.Buffer.Glyphs[position]);

        if (set < 0)
            return false;

        ReadOnlySpan<byte> span = Table.Span;
        int choice = session.FeatureValue - 1;

        if (choice < 0 || choice >= BigEndian.UInt16(span, set))
            return false;

        if (!session.TryReplace(position, BigEndian.UInt16(span, set + 2 + (choice * 2))))
            return false;

        next = position + 1;
        return true;
    }
}
