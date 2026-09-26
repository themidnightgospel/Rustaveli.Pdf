namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The Mac OS Roman character set, which old fonts use for Macintosh-platform names and character maps.
/// </summary>
/// <remarks>
/// Written out rather than taken from <see cref="System.Text.Encoding"/>: .NET Core ships Mac Roman only through
/// the code-pages provider package, and the core takes no package dependencies.
/// </remarks>
internal static class MacRoman
{
    /// <summary>Unicode for bytes 0x80 to 0xFF; the lower half is ASCII.</summary>
    private const string UpperHalf =
        "ÄÅÇÉÑÖÜáàâäãåçéè" +
        "êëíìîïñóòôöõúùûü" +
        "†°¢£§•¶ß®©™´¨≠ÆØ" +
        "∞±≤≥¥µ∂∑∏π∫ªºΩæø" +
        "¿¡¬√ƒ≈∆«»… ÀÃÕŒœ" +
        "–—“”‘’÷◊ÿŸ⁄€‹›ﬁﬂ" +
        "‡·‚„‰ÂÊÁËÈÍÎÏÌÓÔ" +
        "ÒÚÛÙıˆ˜¯˘˙˚¸˝˛ˇ";

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        char[] characters = new char[bytes.Length];

        for (int index = 0; index < bytes.Length; index++)
            characters[index] = ToUnicode(bytes[index]);

        return new string(characters);
    }

    public static char ToUnicode(byte value) => value < 0x80 ? (char)value : UpperHalf[value - 0x80];

    /// <summary>The Mac Roman byte for a Unicode code point, or -1 when the character set lacks it.</summary>
    public static int Encode(int codepoint)
    {
        if (codepoint >= 0 && codepoint < 0x80)
            return codepoint;

        int index = codepoint is > 0x7F and <= char.MaxValue ? UpperHalf.IndexOf((char)codepoint) : -1;

        return index < 0 ? -1 : index + 0x80;
    }
}
