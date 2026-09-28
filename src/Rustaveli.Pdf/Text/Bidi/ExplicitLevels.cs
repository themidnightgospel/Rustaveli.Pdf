namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// Rules X1 to X8 of UAX #9: the levels that embeddings, overrides and isolates set, before any character's own
/// direction is considered.
/// </summary>
/// <remarks>
/// Characters that rule X9 removes (embedding and override controls, boundary neutrals) are given no level here: they
/// take part in nothing that follows, and are given the level of the character before them once the rest is resolved.
/// </remarks>
internal static class ExplicitLevels
{
    /// <summary>The deepest level an embedding or isolate may open (BD2).</summary>
    public const int MaxDepth = 125;

    /// <summary>
    /// Sets <paramref name="levels"/> to each character's explicit level, and resets the types of characters inside an
    /// override to its direction.
    /// </summary>
    /// <param name="classes">The characters' own classes, which rule X5c reads to decide the direction of an FSI.</param>
    /// <param name="isolates">
    /// For each isolate initiator, its matching PDI, or the paragraph's length when it has none (BD9); empty when there
    /// are no isolates.
    /// </param>
    /// <param name="types">The classes being resolved: read, and reset inside overrides.</param>
    /// <param name="levels">Receives the levels.</param>
    /// <param name="paragraphLevel">The paragraph embedding level.</param>
    public static void Resolve(
        ReadOnlySpan<BidiClass> classes, ReadOnlySpan<int> isolates, Span<BidiClass> types, Span<byte> levels,
        int paragraphLevel)
    {
        // The directional status stack (X1): one entry per open embedding, override or isolate, above the paragraph's.
        Span<byte> stackLevels = stackalloc byte[MaxDepth + 2];
        Span<BidiClass> stackOverrides = stackalloc BidiClass[MaxDepth + 2];
        Span<bool> stackIsolates = stackalloc bool[MaxDepth + 2];

        int depth = 1;
        stackLevels[0] = (byte)paragraphLevel;
        stackOverrides[0] = BidiClass.ON;

        int overflowIsolates = 0;
        int overflowEmbeddings = 0;
        int validIsolates = 0;

        for (int index = 0; index < types.Length; index++)
        {
            BidiClass type = types[index];
            int top = depth - 1;

            switch (type)
            {
                case BidiClass.RLE:
                case BidiClass.LRE:
                case BidiClass.RLO:
                case BidiClass.LRO:
                {
                    // X2 to X5.
                    int level = NextLevel(stackLevels[top], rightToLeft: type is BidiClass.RLE or BidiClass.RLO);

                    if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        stackLevels[depth] = (byte)level;
                        stackOverrides[depth] = type switch
                        {
                            BidiClass.RLO => BidiClass.R,
                            BidiClass.LRO => BidiClass.L,
                            _ => BidiClass.ON,
                        };
                        stackIsolates[depth] = false;
                        depth++;
                    }
                    else if (overflowIsolates == 0)
                    {
                        overflowEmbeddings++;
                    }

                    break;
                }

                case BidiClass.RLI:
                case BidiClass.LRI:
                case BidiClass.FSI:
                {
                    // X5a to X5c. The initiator itself stays outside the isolate it opens.
                    levels[index] = stackLevels[top];
                    if (stackOverrides[top] != BidiClass.ON)
                        types[index] = stackOverrides[top];

                    // X5c: an FSI takes the direction of the first strong character between it and its matching PDI.
                    bool rightToLeft = type == BidiClass.RLI ||
                                       (type == BidiClass.FSI &&
                                        FirstStrong(classes, isolates, index + 1, isolates[index]) == BidiClass.R);
                    int level = NextLevel(stackLevels[top], rightToLeft);

                    if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        validIsolates++;
                        stackLevels[depth] = (byte)level;
                        stackOverrides[depth] = BidiClass.ON;
                        stackIsolates[depth] = true;
                        depth++;
                    }
                    else
                    {
                        overflowIsolates++;
                    }

                    break;
                }

                case BidiClass.PDI:
                {
                    // X6a. A PDI closes its isolate and every embedding left open inside it.
                    if (overflowIsolates > 0)
                    {
                        overflowIsolates--;
                    }
                    else if (validIsolates > 0)
                    {
                        overflowEmbeddings = 0;
                        while (!stackIsolates[depth - 1])
                            depth--;

                        depth--;
                        validIsolates--;
                    }

                    top = depth - 1;
                    levels[index] = stackLevels[top];
                    if (stackOverrides[top] != BidiClass.ON)
                        types[index] = stackOverrides[top];

                    break;
                }

                case BidiClass.PDF:
                    // X7. A PDF inside an isolate cannot close an embedding opened outside it, whether the isolate
                    // overflowed or not.
                    if (overflowIsolates == 0)
                    {
                        if (overflowEmbeddings > 0)
                            overflowEmbeddings--;
                        else if (!stackIsolates[top] && depth >= 2)
                            depth--;
                    }

                    break;

                case BidiClass.B:
                    // X8: a paragraph separator is outside every embedding.
                    levels[index] = (byte)paragraphLevel;
                    break;

                case BidiClass.BN:
                    // Removed by X9, and never overridden: an override must not turn it into a character that stays.
                    break;

                default:
                    // X6.
                    levels[index] = stackLevels[top];
                    if (stackOverrides[top] != BidiClass.ON)
                        types[index] = stackOverrides[top];

                    break;
            }
        }
    }

    /// <summary>
    /// The first strong direction in the paragraph from <paramref name="start"/> up to <paramref name="end"/>, passing
    /// over isolates (rule P2): <see cref="BidiClass.L"/>, <see cref="BidiClass.R"/> (for R and AL alike), or
    /// <see cref="BidiClass.ON"/> when there is none.
    /// </summary>
    public static BidiClass FirstStrong(ReadOnlySpan<BidiClass> classes, ReadOnlySpan<int> isolates, int start, int end)
    {
        for (int index = start; index < end; index++)
        {
            switch (classes[index])
            {
                case BidiClass.L:
                    return BidiClass.L;

                case BidiClass.R:
                case BidiClass.AL:
                    return BidiClass.R;

                case BidiClass.LRI:
                case BidiClass.RLI:
                case BidiClass.FSI:
                    // On past the isolate: to its matching PDI, or the end of the paragraph when it has none.
                    index = isolates[index];
                    break;
            }
        }

        return BidiClass.ON;
    }

    // The least odd level above the current one for a right-to-left scope, the least even level for a left-to-right one.
    private static int NextLevel(int level, bool rightToLeft) => rightToLeft ? (level + 1) | 1 : (level + 2) & ~1;
}
