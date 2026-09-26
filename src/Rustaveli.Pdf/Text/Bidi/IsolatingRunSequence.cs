namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// The rules UAX #9 applies within one isolating run sequence: weak types (W1 to W7), bracket pairs (N0), neutrals
/// (N1, N2) and implicit levels (I1, I2).
/// </summary>
/// <remarks>
/// Each rule works on the sequence's types copied side by side, so the characters an isolate or a removed control
/// separates in the paragraph are neighbours here, as rule X10 requires. Each rule finishes the whole sequence before
/// the next begins.
/// </remarks>
internal static class IsolatingRunSequence
{
    /// <summary>Rules W1 to W7. <paramref name="sos"/> is the type before the sequence, L or R.</summary>
    public static void ResolveWeakTypes(Span<BidiClass> types, BidiClass sos)
    {
        // W1: a nonspacing mark takes the type of what it follows, except that after an isolate it is a neutral.
        BidiClass previous = sos;
        for (int position = 0; position < types.Length; position++)
        {
            if (types[position] == BidiClass.NSM)
                types[position] = IsIsolateControl(previous) ? BidiClass.ON : previous;

            previous = types[position];
        }

        // W2 and W3 together: a European number after Arabic letters is an Arabic number, then Arabic letters are R.
        // W2 looks back at the letters as they were, so it is tracked before W3 changes them.
        BidiClass strong = sos;
        for (int position = 0; position < types.Length; position++)
        {
            switch (types[position])
            {
                case BidiClass.L:
                case BidiClass.R:
                    strong = types[position];
                    break;

                case BidiClass.AL:
                    strong = BidiClass.AL;
                    types[position] = BidiClass.R;
                    break;

                case BidiClass.EN when strong == BidiClass.AL:
                    types[position] = BidiClass.AN;
                    break;
            }
        }

        // W4: a single separator between two numbers of a kind joins them.
        for (int position = 1; position < types.Length - 1; position++)
        {
            BidiClass type = types[position];
            BidiClass before = types[position - 1];
            BidiClass after = types[position + 1];

            if (type is BidiClass.ES or BidiClass.CS && before == BidiClass.EN && after == BidiClass.EN)
                types[position] = BidiClass.EN;
            else if (type == BidiClass.CS && before == BidiClass.AN && after == BidiClass.AN)
                types[position] = BidiClass.AN;
        }

        // W5: terminators next to a European number belong to it, as a currency or percent sign does.
        for (int position = 0; position < types.Length; position++)
        {
            if (types[position] != BidiClass.ET)
                continue;

            int end = position + 1;
            while (end < types.Length && types[end] == BidiClass.ET)
                end++;

            if ((position > 0 && types[position - 1] == BidiClass.EN) || (end < types.Length && types[end] == BidiClass.EN))
                types.Slice(position, end - position).Fill(BidiClass.EN);

            position = end;
        }

        // W6: separators and terminators left over are neutrals.
        for (int position = 0; position < types.Length; position++)
        {
            if (types[position] is BidiClass.ES or BidiClass.ET or BidiClass.CS)
                types[position] = BidiClass.ON;
        }

        // W7: a European number in left-to-right context is set as left-to-right text.
        strong = sos;
        for (int position = 0; position < types.Length; position++)
        {
            BidiClass type = types[position];
            if (type is BidiClass.L or BidiClass.R)
                strong = type;
            else if (type == BidiClass.EN && strong == BidiClass.L)
                types[position] = BidiClass.L;
        }
    }

    /// <summary>
    /// Rule N0: both brackets of a pair take one direction, decided by what they enclose and, failing that, by what
    /// comes before them.
    /// </summary>
    /// <param name="types">The sequence's types, after the weak rules.</param>
    /// <param name="indices">Where each position of the sequence is in the paragraph.</param>
    /// <param name="text">The paragraph's text; empty when the paragraph was given as classes, which have no brackets.</param>
    /// <param name="original">The paragraph's types before the weak rules, indexed by position in the paragraph.</param>
    /// <param name="sos">The type before the sequence.</param>
    /// <param name="embedding">The sequence's embedding direction, L or R.</param>
    /// <param name="closings">Working space as long as the sequence.</param>
    public static void ResolveBracketPairs(
        Span<BidiClass> types, ReadOnlySpan<int> indices, ReadOnlySpan<char> text, ReadOnlySpan<BidiClass> original,
        BidiClass sos, BidiClass embedding, Span<int> closings)
    {
        if (text.IsEmpty || !BracketPairs.Find(types, indices, text, closings))
            return;

        // Pairs are resolved in the order of their opening brackets, each seeing the directions given before it.
        for (int opening = 0; opening < types.Length; opening++)
        {
            int closing = closings[opening];
            if (closing < 0)
                continue;

            BidiClass direction = PairDirection(types, opening, closing, sos, embedding);
            if (direction == BidiClass.ON)
                continue;

            SetBracket(types, indices, original, opening, direction);
            SetBracket(types, indices, original, closing, direction);
        }
    }

    /// <summary>
    /// Rules N1 and N2: a run of neutrals between two characters of one direction takes it, and otherwise takes the
    /// embedding direction. Numbers count as right-to-left here.
    /// </summary>
    public static void ResolveNeutralTypes(Span<BidiClass> types, BidiClass sos, BidiClass eos, BidiClass embedding)
    {
        for (int position = 0; position < types.Length; position++)
        {
            if (!IsNeutralOrIsolate(types[position]))
                continue;

            int end = position + 1;
            while (end < types.Length && IsNeutralOrIsolate(types[end]))
                end++;

            BidiClass before = position == 0 ? sos : StrongDirection(types[position - 1]);
            BidiClass after = end == types.Length ? eos : StrongDirection(types[end]);

            types.Slice(position, end - position).Fill(before == after ? before : embedding);
            position = end;
        }
    }

    /// <summary>
    /// Rules I1 and I2: each character's level from the sequence's level and its resolved type, written to
    /// <paramref name="levels"/> at its position in the paragraph.
    /// </summary>
    public static void ResolveImplicitLevels(
        ReadOnlySpan<BidiClass> types, ReadOnlySpan<int> indices, Span<byte> levels, int level)
    {
        bool odd = (level & 1) != 0;

        for (int position = 0; position < types.Length; position++)
        {
            BidiClass type = types[position];
            int raise = odd
                ? (type == BidiClass.R ? 0 : 1)
                : type switch
                {
                    BidiClass.R => 1,
                    BidiClass.L => 0,
                    _ => 2,
                };

            levels[indices[position]] = (byte)(level + raise);
        }
    }

    // What a pair encloses decides it when any of it runs in the embedding direction; when all of it runs the other
    // way, the pair follows it only if the text before the pair does too. ON when the pair encloses nothing strong.
    private static BidiClass PairDirection(
        ReadOnlySpan<BidiClass> types, int opening, int closing, BidiClass sos, BidiClass embedding)
    {
        bool opposite = false;
        for (int position = opening + 1; position < closing; position++)
        {
            BidiClass direction = StrongDirection(types[position]);
            if (direction == embedding)
                return embedding;

            opposite |= direction != BidiClass.ON;
        }

        if (!opposite)
            return BidiClass.ON;

        for (int position = opening - 1; position >= 0; position--)
        {
            BidiClass direction = StrongDirection(types[position]);
            if (direction != BidiClass.ON)
                return direction;
        }

        return sos;
    }

    // A bracket's direction passes to the nonspacing marks after it, which W1 had given the bracket's neutral type.
    private static void SetBracket(
        Span<BidiClass> types, ReadOnlySpan<int> indices, ReadOnlySpan<BidiClass> original, int position,
        BidiClass direction)
    {
        types[position] = direction;

        for (int mark = position + 1; mark < types.Length && original[indices[mark]] == BidiClass.NSM; mark++)
            types[mark] = direction;
    }

    // The direction a resolved type counts as beside neutrals and inside brackets: numbers count as right-to-left.
    private static BidiClass StrongDirection(BidiClass type) => type switch
    {
        BidiClass.L => BidiClass.L,
        BidiClass.R or BidiClass.EN or BidiClass.AN => BidiClass.R,
        _ => BidiClass.ON,
    };

    private static bool IsNeutralOrIsolate(BidiClass type) =>
        type is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON || IsIsolateControl(type);

    private static bool IsIsolateControl(BidiClass type) =>
        type is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;
}
