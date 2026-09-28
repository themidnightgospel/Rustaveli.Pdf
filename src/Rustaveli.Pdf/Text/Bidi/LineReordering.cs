namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// Rules L1 and L2 of UAX #9, which act on one line once the paragraph has been broken into lines.
/// </summary>
internal static class LineReordering
{
    // Marks a removed control whose level L1 has not settled; no resolved level comes near it.
    private const byte Unsettled = byte.MaxValue;

    /// <summary>
    /// Rule L1: separators, and white space and isolate controls before a separator or at the end of the line, go
    /// back to the paragraph level, so that trailing space sits at the line's end in the paragraph's direction.
    /// </summary>
    /// <param name="classes">The characters' own classes, not the resolved ones.</param>
    /// <param name="levels">The line's resolved levels, adjusted in place.</param>
    /// <param name="paragraphLevel">The paragraph embedding level.</param>
    /// <remarks>
    /// The controls rule X9 removed count as white space here. Elsewhere on the line they take the level of the
    /// character before them, or the paragraph level at the start of the line, so that they never split a run.
    /// </remarks>
    public static void ResetWhitespace(ReadOnlySpan<BidiClass> classes, Span<byte> levels, int paragraphLevel)
    {
        bool trailing = true;

        for (int index = classes.Length - 1; index >= 0; index--)
        {
            BidiClass type = classes[index];

            if (type is BidiClass.S or BidiClass.B)
            {
                levels[index] = (byte)paragraphLevel;
                trailing = true;
            }
            else if (type is BidiClass.WS or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI)
            {
                if (trailing)
                    levels[index] = (byte)paragraphLevel;
            }
            else if (BidiParagraph.IsRemovedByX9(type))
            {
                levels[index] = trailing ? (byte)paragraphLevel : Unsettled;
            }
            else
            {
                trailing = false;
            }
        }

        for (int index = 0; index < levels.Length; index++)
        {
            if (levels[index] == Unsettled)
                levels[index] = index == 0 ? (byte)paragraphLevel : levels[index - 1];
        }
    }

    /// <summary>
    /// Rule L2: the line's level runs in the order they are displayed, left to right.
    /// </summary>
    /// <param name="levels">The line's levels, after <see cref="ResetWhitespace"/>.</param>
    /// <param name="lineStart">Where the line starts in the paragraph, which the runs' positions are measured from.</param>
    /// <param name="runs">Receives the runs, replacing what it held.</param>
    /// <remarks>
    /// Reversing whole runs rather than characters gives the same order, since a run's characters all share one level:
    /// a run is reversed as a unit at every level up to its own, and its characters end up right to left exactly when
    /// that level is odd, which <see cref="BidiRun.IsRightToLeft"/> reports.
    /// </remarks>
    public static void Reorder(ReadOnlySpan<byte> levels, int lineStart, List<BidiRun> runs)
    {
        runs.Clear();

        int highest = 0;
        int lowestOdd = int.MaxValue;
        int start = 0;

        for (int index = 1; index <= levels.Length; index++)
        {
            if (index < levels.Length && levels[index] == levels[start])
                continue;

            int level = levels[start];
            runs.Add(new BidiRun(lineStart + start, index - start, level));
            highest = Math.Max(highest, level);
            if ((level & 1) != 0)
                lowestOdd = Math.Min(lowestOdd, level);

            start = index;
        }

        // From the highest level down to the lowest odd one, levels in between included, every stretch of runs at that
        // level or above is reversed.
        for (int level = highest; level >= lowestOdd; level--)
        {
            for (int run = 0; run < runs.Count; run++)
            {
                if (runs[run].Level < level)
                    continue;

                int end = run + 1;
                while (end < runs.Count && runs[end].Level >= level)
                    end++;

                runs.Reverse(run, end - run);
                run = end;
            }
        }
    }
}
