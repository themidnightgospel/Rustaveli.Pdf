namespace Rustaveli.Pdf.Text.LineBreaking;

/// <summary>
/// What the line breaking rules need to know about one code point: its class, and the two other Unicode properties
/// some rules consult.
/// </summary>
/// <remarks>
/// A single byte, exactly as <see cref="LineBreakTable"/> stores it, so nothing is unpacked until a rule asks. The
/// default value is the start of the text.
/// </remarks>
internal readonly struct LineBreakProperties
{
    private const int ClassMask = 0x3F;
    private const int EastAsianFlag = 0x40;
    private const int UnassignedPictographicFlag = 0x80;

    private readonly byte _value;

    private LineBreakProperties(byte value) => _value = value;

    /// <summary>
    /// What LB10 makes of a combining mark or joiner with nothing to attach to: the properties of U+0041, a plain
    /// letter that is neither East Asian nor pictographic.
    /// </summary>
    public static LineBreakProperties Alphabetic => From(LineBreakClass.AL);

    /// <summary>What follows the last character, for the rules that look ahead.</summary>
    public static LineBreakProperties EndOfText => From(LineBreakClass.Eot);

    public LineBreakClass Class => (LineBreakClass)(_value & ClassMask);

    /// <summary>
    /// East_Asian_Width is Fullwidth, Wide or Halfwidth: the <c>$EastAsian</c> set of rules LB19a, LB21a and LB30.
    /// </summary>
    public bool IsEastAsian => (_value & EastAsianFlag) != 0;

    /// <summary>
    /// An Extended_Pictographic code point not yet assigned, which LB30b keeps with a following skin tone modifier
    /// so that emoji from a newer Unicode version than this table's still hold together.
    /// </summary>
    public bool IsUnassignedPictographic => (_value & UnassignedPictographicFlag) != 0;

    /// <summary>A class with neither flag set.</summary>
    public static LineBreakProperties From(LineBreakClass kind) => new LineBreakProperties((byte)kind);

    public static LineBreakProperties Of(int codepoint)
    {
        // Latin text is nearly all of what gets measured, and below this limit the leaves can be read by code point.
        if (codepoint < LineBreakTable.DirectLimit)
            return new LineBreakProperties(LineBreakTable.Leaves[codepoint]);

        int chunk = LineBreakTable.Top[codepoint >> LineBreakTable.TopShift];
        int leaf = LineBreakTable.Middle[
            (chunk << (LineBreakTable.TopShift - LineBreakTable.MiddleShift)) |
            ((codepoint >> LineBreakTable.MiddleShift) & LineBreakTable.MiddleMask)];

        return new LineBreakProperties(
            LineBreakTable.Leaves[(leaf << LineBreakTable.MiddleShift) | (codepoint & LineBreakTable.LeafMask)]);
    }
}
