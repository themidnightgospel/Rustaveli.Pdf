namespace Rustaveli.Pdf.Text.Bidi;

/// <summary>
/// Looks up the bidirectional properties of a code point: its class, its paired bracket and its mirrored form.
/// </summary>
internal static class BidiCharacter
{
    private const int MaxCodepoint = 0x10FFFF;

    /// <summary>
    /// The class of <paramref name="codepoint"/>. Unassigned code points have the class the Unicode Character Database
    /// gives them by default: right-to-left in the blocks set aside for right-to-left scripts, left-to-right elsewhere.
    /// A value outside the code space is taken as left-to-right, the class of most of it.
    /// </summary>
    public static BidiClass ClassOf(int codepoint)
    {
        if ((uint)codepoint > MaxCodepoint)
            return BidiClass.L;

        const int Stage2Mask = (1 << BidiCharacterTables.ClassStage2Bits) - 1;
        const int Stage3Mask = (1 << BidiCharacterTables.ClassStage3Bits) - 1;

        int run = BidiCharacterTables.ClassStage1[
            codepoint >> (BidiCharacterTables.ClassStage2Bits + BidiCharacterTables.ClassStage3Bits)];
        int block = BidiCharacterTables.ClassStage2[
            (run << BidiCharacterTables.ClassStage2Bits) + ((codepoint >> BidiCharacterTables.ClassStage3Bits) & Stage2Mask)];

        return (BidiClass)BidiCharacterTables.ClassStage3[(block << BidiCharacterTables.ClassStage3Bits) + (codepoint & Stage3Mask)];
    }

    /// <summary>
    /// The character whose glyph mirrors that of <paramref name="codepoint"/> (Bidi_Mirroring_Glyph), or the code point
    /// itself when it has none. Rule L4 draws a character this way when its resolved level is odd.
    /// </summary>
    public static int Mirror(int codepoint)
    {
        int record = Find(BidiCharacterTables.Mirrors, BidiCharacterTables.MirrorRecordSize, codepoint);

        return record < 0 ? codepoint : ReadUInt16(BidiCharacterTables.Mirrors, record + 2);
    }

    /// <summary>
    /// Whether <paramref name="codepoint"/> opens or closes a bracket pair, and the bracket that pairs with it
    /// (Bidi_Paired_Bracket); <paramref name="pair"/> is the code point itself when it is no bracket.
    /// </summary>
    public static BidiBracketType BracketOf(int codepoint, out int pair)
    {
        ReadOnlySpan<byte> brackets = BidiCharacterTables.Brackets;
        int record = Find(brackets, BidiCharacterTables.BracketRecordSize, codepoint);

        if (record < 0)
        {
            pair = codepoint;
            return BidiBracketType.None;
        }

        pair = ReadUInt16(brackets, record + 2);
        return (BidiBracketType)brackets[record + 4];
    }

    // The offset of the record whose first field is the code point, by binary search, or -1. Every record starts with
    // a code point in the Basic Multilingual Plane, so a larger value is simply absent.
    private static int Find(ReadOnlySpan<byte> records, int recordSize, int codepoint)
    {
        int low = 0;
        int high = (records.Length / recordSize) - 1;

        while (low <= high)
        {
            int middle = (low + high) >> 1;
            int found = ReadUInt16(records, middle * recordSize);

            if (found == codepoint)
                return middle * recordSize;

            if (found < codepoint)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return -1;
    }

    private static int ReadUInt16(ReadOnlySpan<byte> data, int offset) => (data[offset] << 8) | data[offset + 1];
}
