namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// A paragraph's text resolved by the Unicode Bidirectional Algorithm (UAX #9): its direction, the level of every
/// character, and for any line of it the order its runs are displayed in.
/// </summary>
/// <remarks>
/// <para>
/// Levels are resolved once for the whole paragraph, since a character's direction can depend on text lines away. The
/// paragraph is then broken into lines on the widths of its characters in logical order, and each line is reordered
/// on its own (rules L1 and L2): trailing white space goes to the line's end, and runs are reversed.
/// </para>
/// <para>
/// Positions and levels are per UTF-16 code unit; both halves of a surrogate pair share the level of their character.
/// The text is one paragraph: a paragraph separator inside it is given the paragraph level, as rule X8 says, but does
/// not start a paragraph of its own direction.
/// </para>
/// <para>
/// Most text is left-to-right throughout, with no directional controls. That is recognised by a scan that looks up no
/// table below the Hebrew block, and such a paragraph keeps no per-character state: every level is zero and a line is
/// one run.
/// </para>
/// </remarks>
internal sealed class BidiParagraph
{
    // Nothing before the Hebrew block is right-to-left, an Arabic number or a directional control.
    private const char FirstRightToLeftCandidate = '\u0590';

    // Lines this long or shorter reorder with their levels on the stack.
    private const int StackLineLength = 256;

    private readonly BidiClass[]? _classes;
    private readonly byte[]? _levels;

    /// <summary>Resolves <paramref name="text"/> as one paragraph.</summary>
    public BidiParagraph(ReadOnlySpan<char> text, BidiDirection direction = BidiDirection.Auto)
    {
        Length = text.Length;

        if (direction != BidiDirection.RightToLeft && StaysLeftToRight(text))
        {
            IsLeftToRightOnly = true;
            return;
        }

        BidiClass[] classes = new BidiClass[text.Length];
        BidiClass[] types = new BidiClass[text.Length];

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];

            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                BidiClass type = BidiCharacter.ClassOf(char.ConvertToUtf32(character, text[index + 1]));
                classes[index] = type;
                classes[index + 1] = type;
                types[index] = type;

                // The low surrogate is passed over like a boundary neutral, which leaves it the level of the character
                // before it: its own high surrogate.
                types[index + 1] = BidiClass.BN;
                index++;
            }
            else
            {
                classes[index] = BidiCharacter.ClassOf(character);
                types[index] = classes[index];
            }
        }

        _classes = classes;
        (ParagraphLevel, _levels) = Resolve(text, classes, types, direction);
        IsLeftToRightOnly = !_levels.AsSpan().ContainsAnyExcept((byte)0);
    }

    /// <summary>
    /// Resolves a paragraph given as the classes of its characters rather than the characters themselves, as the
    /// Unicode conformance test BidiTest.txt states its cases. With no characters there are no bracket pairs.
    /// </summary>
    internal BidiParagraph(ReadOnlySpan<BidiClass> classes, BidiDirection direction = BidiDirection.Auto)
    {
        Length = classes.Length;

        if (direction != BidiDirection.RightToLeft && StaysLeftToRight(classes))
        {
            IsLeftToRightOnly = true;
            return;
        }

        _classes = classes.ToArray();
        (ParagraphLevel, _levels) = Resolve([], _classes, classes.ToArray(), direction);
        IsLeftToRightOnly = !_levels.AsSpan().ContainsAnyExcept((byte)0);
    }

    /// <summary>The paragraph's length in UTF-16 code units.</summary>
    public int Length { get; }

    /// <summary>The paragraph embedding level: 0 for a left-to-right paragraph, 1 for a right-to-left one.</summary>
    public int ParagraphLevel { get; }

    /// <summary>
    /// Whether every character resolved to level 0, so that no line of the paragraph needs reordering.
    /// </summary>
    public bool IsLeftToRightOnly { get; }

    /// <summary>
    /// The resolved level of the character at <paramref name="index"/>, before any line is reordered. Controls that
    /// rule X9 removes have the level of the character before them.
    /// </summary>
    public int GetLevel(int index)
    {
        if ((uint)index >= (uint)Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is outside the paragraph.");

        return _levels is null ? 0 : _levels[index];
    }

    /// <summary>
    /// The levels of the line from <paramref name="start"/> for <paramref name="length"/> code units, after rule L1 has
    /// returned its separators and trailing white space to the paragraph level.
    /// </summary>
    public void GetLineLevels(int start, int length, Span<byte> levels)
    {
        CheckLine(start, length);
        if (levels.Length < length)
            throw new ArgumentException("The span is shorter than the line.", nameof(levels));

        Span<byte> line = levels.Slice(0, length);

        if (_levels is null)
        {
            line.Clear();
            return;
        }

        _levels.AsSpan(start, length).CopyTo(line);
        LineReordering.ResetWhitespace(_classes.AsSpan(start, length), line, ParagraphLevel);
    }

    /// <summary>
    /// The runs of the line from <paramref name="start"/> for <paramref name="length"/> code units, in the order they
    /// are displayed from left to right (rules L1 and L2).
    /// </summary>
    /// <param name="start">Where the line starts in the paragraph.</param>
    /// <param name="length">The line's length, including any white space it ends with.</param>
    /// <param name="runs">Receives the runs, replacing what it held; reuse one list across lines to allocate nothing.</param>
    public void GetVisualRuns(int start, int length, List<BidiRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        CheckLine(start, length);

        runs.Clear();
        if (length == 0)
            return;

        if (_levels is null)
        {
            runs.Add(new BidiRun(start, length, 0));
            return;
        }

        byte[]? buffer = length > StackLineLength ? new byte[length] : null;
        Span<byte> levels = buffer ?? stackalloc byte[StackLineLength];

        GetLineLevels(start, length, levels);
        LineReordering.Reorder(levels.Slice(0, length), start, runs);
    }

    /// <summary>Whether rule X9 removes characters of this class from the rest of the algorithm.</summary>
    internal static bool IsRemovedByX9(BidiClass type) =>
        type is BidiClass.BN or BidiClass.LRE or BidiClass.RLE or BidiClass.LRO or BidiClass.RLO or BidiClass.PDF;

    private void CheckLine(int start, int length)
    {
        if ((uint)start > (uint)Length)
            throw new ArgumentOutOfRangeException(nameof(start), start, "The line starts outside the paragraph.");

        if ((uint)length > (uint)(Length - start))
            throw new ArgumentOutOfRangeException(nameof(length), length, "The line ends outside the paragraph.");
    }

    private static bool StaysLeftToRight(ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character < FirstRightToLeftCandidate)
                continue;

            int codepoint = character;
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                codepoint = char.ConvertToUtf32(character, text[index + 1]);
                index++;
            }

            if (!StaysLeftToRight(BidiCharacter.ClassOf(codepoint)))
                return false;
        }

        return true;
    }

    private static bool StaysLeftToRight(ReadOnlySpan<BidiClass> classes)
    {
        foreach (BidiClass type in classes)
        {
            if (!StaysLeftToRight(type))
                return false;
        }

        return true;
    }

    // Without right-to-left characters, Arabic numbers or directional controls, a paragraph that is not forced right to
    // left resolves to level 0 throughout: European numbers find only left-to-right context (W7) and neutrals only
    // left-to-right neighbours (N1).
    private static bool StaysLeftToRight(BidiClass type) =>
        type is not (BidiClass.R or BidiClass.AL or BidiClass.AN) && type < BidiClass.LRE;

    private static (int ParagraphLevel, byte[] Levels) Resolve(
        ReadOnlySpan<char> text, BidiClass[] classes, BidiClass[] types, BidiDirection direction)
    {
        int length = classes.Length;
        int[] isolates = MatchIsolates(classes);

        // P2 and P3, unless the direction is given (HL1).
        int paragraphLevel = direction switch
        {
            BidiDirection.LeftToRight => 0,
            BidiDirection.RightToLeft => 1,
            _ => ExplicitLevels.FirstStrong(classes, isolates, 0, length) == BidiClass.R ? 1 : 0,
        };

        // X1 to X8 set the explicit levels, which rule X10 still needs after I1 and I2 have begun raising levels.
        byte[] explicitLevels = new byte[length];
        ExplicitLevels.Resolve(classes, isolates, types, explicitLevels, paragraphLevel);

        byte[] levels = new byte[length];
        int[] order = new int[length];
        BidiClass[] sequenceTypes = new BidiClass[length];
        int[] closings = new int[length];
        int used = 0;

        // X10: each level run that does not continue an isolating run sequence starts one.
        int previous = -1;
        for (int index = 0; index < length; index++)
        {
            if (IsRemovedByX9(types[index]))
                continue;

            bool startsRun = previous < 0 || explicitLevels[previous] != explicitLevels[index];
            previous = index;

            if (!startsRun || ContinuesSequence(classes, types, explicitLevels, isolates, index))
                continue;

            int count = CollectSequence(classes, types, explicitLevels, isolates, index, order.AsSpan(used));
            Span<int> indices = order.AsSpan(used, count);
            Span<BidiClass> sequence = sequenceTypes.AsSpan(used, count);
            Span<int> pairs = closings.AsSpan(used, count);
            used += count;

            int level = explicitLevels[index];
            BidiClass embedding = (level & 1) == 0 ? BidiClass.L : BidiClass.R;
            BidiClass sos = Boundary(level, KeptBefore(types, index) is int before ? explicitLevels[before] : paragraphLevel);

            int last = indices[count - 1];
            BidiClass eos = Boundary(
                level,
                !IsIsolateInitiator(classes[last]) && KeptAfter(types, last) is int after
                    ? explicitLevels[after]
                    : paragraphLevel);

            for (int position = 0; position < count; position++)
                sequence[position] = types[indices[position]];

            IsolatingRunSequence.ResolveWeakTypes(sequence, sos);
            IsolatingRunSequence.ResolveBracketPairs(sequence, indices, text, types, sos, embedding, pairs);
            IsolatingRunSequence.ResolveNeutralTypes(sequence, sos, eos, embedding);
            IsolatingRunSequence.ResolveImplicitLevels(sequence, indices, levels, level);
        }

        // The controls X9 removed sit with the character before them, so that they never split a run.
        for (int index = 0; index < length; index++)
        {
            if (IsRemovedByX9(types[index]))
                levels[index] = index == 0 ? (byte)paragraphLevel : levels[index - 1];
        }

        return (paragraphLevel, levels);
    }

    // BD9: each isolate initiator's matching PDI, and each matched PDI's initiator; -1 where there is none. Empty when
    // the paragraph has no isolate controls, since nothing reads it then.
    private static int[] MatchIsolates(ReadOnlySpan<BidiClass> classes)
    {
        int[]? matches = null;
        int[]? open = null;
        int depth = 0;

        for (int index = 0; index < classes.Length; index++)
        {
            BidiClass type = classes[index];
            if (!IsIsolateInitiator(type) && type != BidiClass.PDI)
                continue;

            if (matches is null)
            {
                matches = new int[classes.Length];
                matches.AsSpan().Fill(-1);
                open = new int[classes.Length];
            }

            if (type != BidiClass.PDI)
            {
                open![depth++] = index;
            }
            else if (depth > 0)
            {
                int initiator = open![--depth];
                matches[initiator] = index;
                matches[index] = initiator;
            }
        }

        return matches ?? [];
    }

    // A level run that starts with the PDI matching an initiator that ends a level run continues the initiator's
    // sequence rather than starting one of its own (BD13).
    private static bool ContinuesSequence(
        ReadOnlySpan<BidiClass> classes, ReadOnlySpan<BidiClass> types, ReadOnlySpan<byte> explicitLevels,
        ReadOnlySpan<int> isolates, int runStart) =>
        classes[runStart] == BidiClass.PDI && isolates[runStart] >= 0 && EndsRun(types, explicitLevels, isolates[runStart]);

    private static int CollectSequence(
        ReadOnlySpan<BidiClass> classes, ReadOnlySpan<BidiClass> types, ReadOnlySpan<byte> explicitLevels,
        ReadOnlySpan<int> isolates, int runStart, Span<int> order)
    {
        int count = 0;
        int start = runStart;

        while (true)
        {
            int level = explicitLevels[start];
            int last = start;

            for (int index = start; index < types.Length; index++)
            {
                if (IsRemovedByX9(types[index]))
                    continue;

                if (explicitLevels[index] != level)
                    break;

                order[count++] = index;
                last = index;
            }

            // An isolate initiator ending the run carries the sequence on to the run its matching PDI starts. Both
            // always hold for a matched initiator unless a paragraph separator inside the isolate breaks its level.
            if (!IsIsolateInitiator(classes[last]) || isolates[last] < 0 || !StartsRun(types, explicitLevels, isolates[last]))
                return count;

            start = isolates[last];
        }
    }

    // Whether a matched PDI starts a level run, and a matched isolate initiator ends one. Neither is at the edge of the
    // paragraph: each has its partner, which X9 never removes, on the far side.
    private static bool StartsRun(ReadOnlySpan<BidiClass> types, ReadOnlySpan<byte> explicitLevels, int pdi) =>
        explicitLevels[KeptBefore(types, pdi)!.Value] != explicitLevels[pdi];

    private static bool EndsRun(ReadOnlySpan<BidiClass> types, ReadOnlySpan<byte> explicitLevels, int initiator) =>
        explicitLevels[KeptAfter(types, initiator)!.Value] != explicitLevels[initiator];

    // sos and eos: the direction of the higher of the levels either side of the sequence's edge.
    private static BidiClass Boundary(int level, int neighbour) =>
        (Math.Max(level, neighbour) & 1) == 0 ? BidiClass.L : BidiClass.R;

    private static int? KeptBefore(ReadOnlySpan<BidiClass> types, int index)
    {
        for (int before = index - 1; before >= 0; before--)
        {
            if (!IsRemovedByX9(types[before]))
                return before;
        }

        return null;
    }

    private static int? KeptAfter(ReadOnlySpan<BidiClass> types, int index)
    {
        for (int after = index + 1; after < types.Length; after++)
        {
            if (!IsRemovedByX9(types[after]))
                return after;
        }

        return null;
    }

    private static bool IsIsolateInitiator(BidiClass type) =>
        type is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;
}
