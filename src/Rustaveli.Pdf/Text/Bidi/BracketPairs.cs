namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// Definition BD16 of UAX #9: which brackets in an isolating run sequence pair up.
/// </summary>
internal static class BracketPairs
{
    /// <summary>
    /// How many opening brackets may wait for their closing one. Text nesting brackets deeper is not ordinary prose,
    /// and the specification caps the stack so that every implementation gives up at the same point.
    /// </summary>
    public const int MaxOpenBrackets = 63;

    /// <summary>What <see cref="Find"/> records at a position that opens no pair.</summary>
    public const int Unpaired = -1;

    /// <summary>
    /// Pairs the brackets of a sequence, recording at each opening bracket's position the position of its closing
    /// bracket, and <see cref="Unpaired"/> at every other position.
    /// </summary>
    /// <param name="types">The sequence's current types: only a bracket still of type ON can pair (BD14, BD15).</param>
    /// <param name="indices">Where each position of the sequence is in the paragraph.</param>
    /// <param name="text">The paragraph's text.</param>
    /// <param name="closings">Receives the pairs; as long as the sequence.</param>
    /// <returns>
    /// False when more brackets are open at once than <see cref="MaxOpenBrackets"/>, in which case the sequence has no
    /// pairs at all.
    /// </returns>
    public static bool Find(
        ReadOnlySpan<BidiClass> types, ReadOnlySpan<int> indices, ReadOnlySpan<char> text, Span<int> closings)
    {
        Span<int> openings = stackalloc int[MaxOpenBrackets];
        Span<int> expected = stackalloc int[MaxOpenBrackets];
        int open = 0;

        closings.Fill(Unpaired);

        for (int position = 0; position < types.Length; position++)
        {
            if (types[position] != BidiClass.ON)
                continue;

            int character = text[indices[position]];
            BidiBracketType bracket = BidiCharacter.BracketOf(character, out int pair);

            if (bracket == BidiBracketType.Open)
            {
                if (open >= MaxOpenBrackets)
                    return false;

                openings[open] = position;
                expected[open] = Canonical(pair);
                open++;
            }
            else if (bracket == BidiBracketType.Close)
            {
                // The nearest opening bracket this one closes, closing any left open inside it; a closing bracket with
                // no opening one to match is not a bracket at all, and leaves the stack as it is.
                int closing = Canonical(character);
                for (int candidate = open - 1; candidate >= 0; candidate--)
                {
                    if (expected[candidate] == closing)
                    {
                        closings[openings[candidate]] = position;
                        open = candidate;
                        break;
                    }
                }
            }
        }

        return true;
    }

    // Brackets pair under canonical equivalence, and the only canonically equivalent brackets are the angle brackets
    // U+2329 and U+232A, which decompose to U+3008 and U+3009; Unicode has undertaken to add no more. Only closing
    // brackets are compared, the one an opening bracket expects and the one found, so only U+232A needs mapping.
    private static int Canonical(int closing) => closing == 0x232A ? 0x3009 : closing;
}
