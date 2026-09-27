namespace Rustaveli.Pdf.Fonts.Substitution;

/// <summary>
/// Where the parts of one contextual rule lie in the GSUB table: three arrays of 16-bit values — glyph ids, classes
/// or coverage offsets, as the subtable's format decides — and the lookup records applied when they match.
/// </summary>
/// <param name="BacktrackCount">How many glyphs must precede the input.</param>
/// <param name="Backtrack">The backtrack values, nearest glyph first.</param>
/// <param name="InputCount">How many glyphs the input has, the first included; 0 in a malformed rule.</param>
/// <param name="Input">
/// The values of the input's second glyph onwards: the first is matched by the subtable's coverage before a rule is
/// tried.
/// </param>
/// <param name="LookaheadCount">How many glyphs must follow the input.</param>
/// <param name="Lookahead">The lookahead values.</param>
/// <param name="LookupCount">How many lookup records the rule has.</param>
/// <param name="Lookups">The lookup records: a sequence index and a lookup index each.</param>
internal readonly record struct ContextRule(
    int BacktrackCount,
    int Backtrack,
    int InputCount,
    int Input,
    int LookaheadCount,
    int Lookahead,
    int LookupCount,
    int Lookups)
{
    /// <summary>A rule of a subtable of format 1 or 2, starting at <paramref name="offset"/>.</summary>
    public static ContextRule Read(ReadOnlySpan<byte> span, int offset, bool chained)
    {
        if (!chained)
        {
            int glyphCount = BigEndian.UInt16(span, offset);
            int input = offset + 4;

            return new ContextRule(
                0, 0, glyphCount, input, 0, 0, BigEndian.UInt16(span, offset + 2), input + ((glyphCount - 1) * 2));
        }

        int backtrackCount = BigEndian.UInt16(span, offset);
        int inputAt = offset + 2 + (backtrackCount * 2);
        int inputCount = BigEndian.UInt16(span, inputAt);
        int lookaheadAt = inputAt + 2 + ((inputCount - 1) * 2);
        int lookaheadCount = BigEndian.UInt16(span, lookaheadAt);
        int lookupsAt = lookaheadAt + 2 + (lookaheadCount * 2);

        return new ContextRule(
            backtrackCount,
            offset + 2,
            inputCount,
            inputAt + 2,
            lookaheadCount,
            lookaheadAt + 2,
            BigEndian.UInt16(span, lookupsAt),
            lookupsAt + 2);
    }
}
