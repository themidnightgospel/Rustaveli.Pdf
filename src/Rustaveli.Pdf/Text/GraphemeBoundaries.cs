using Rustaveli.Pdf.Text.LineBreaking;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// Finds where the characters a reader sees as one begin, taking the text a character at a time: a character with the
/// marks, joiners, variation selectors and emoji modifiers after it, emoji joined by zero width joiners, and a pair of
/// regional indicators making a flag — the extended grapheme clusters of UAX #29, as far as setting type needs them.
/// </summary>
/// <remarks>
/// The properties come from the line breaking table rather than the runtime's character data, which on .NET Framework
/// predates many scripts: combining marks, those of South-East Asian scripts included, are its class CM, and emoji
/// modifiers its class EM.
/// </remarks>
internal struct GraphemeBoundaries
{
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>
    /// Below this, the class CM holds control characters, which a cluster does not extend over; every mark lies above.
    /// </summary>
    private const int FirstMark = 0x0300;

    private int _previous;
    private bool _started;
    private bool _halfFlag;

    /// <summary>Takes the next character, and says whether it begins a new cluster rather than extending the one before.</summary>
    public bool Begins(int codepoint)
    {
        LineBreakClass kind = LineBreakProperties.Of(codepoint).Class;

        bool extends = _started
            && (kind is LineBreakClass.ZWJ or LineBreakClass.EM
                || (kind == LineBreakClass.CM && codepoint >= FirstMark)
                || (_previous == ZeroWidthJoiner && IsPictographic(codepoint))
                || (_halfFlag && kind == LineBreakClass.RI));

        // Regional indicators pair off from the first: the second of each pair completes a flag, the third starts one.
        _halfFlag = kind == LineBreakClass.RI && !_halfFlag;
        _previous = codepoint;
        _started = true;

        return !extends;
    }

    /// <summary>
    /// Takes the characters a glyph stands for, <paramref name="length"/> code units of <paramref name="text"/> from
    /// <paramref name="start"/>, and says whether the first begins a new cluster.
    /// </summary>
    public bool Begins(ReadOnlySpan<char> text, int start, int length)
    {
        bool begins = Begins(CodepointAt(text, start, out int first));

        for (int index = start + first; index < start + length;)
        {
            Begins(CodepointAt(text, index, out int next));
            index += next;
        }

        return begins;
    }

    /// <summary>How many code units the first cluster of <paramref name="text"/> takes; 0 for no text.</summary>
    public static int FirstLength(ReadOnlySpan<char> text)
    {
        GraphemeBoundaries boundaries = default;
        int index = 0;

        while (index < text.Length)
        {
            int codepoint = CodepointAt(text, index, out int length);

            if (boundaries.Begins(codepoint) && index > 0)
                break;

            index += length;
        }

        return index;
    }

    /// <summary>The character at <paramref name="index"/>: a surrogate pair read as one, a lone surrogate as itself.</summary>
    public static int CodepointAt(ReadOnlySpan<char> text, int index, out int length)
    {
        char character = text[index];

        if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(character, text[index + 1]);
        }

        length = 1;
        return character;
    }

    /// <summary>
    /// Whether an emoji a zero width joiner may join to the one before it: the pictographic blocks, as an approximation
    /// of Extended_Pictographic good enough to keep joined sequences together.
    /// </summary>
    private static bool IsPictographic(int codepoint) =>
        codepoint is >= 0x2600 and <= 0x27BF or >= 0x1F000 and <= 0x1FFFF;
}
