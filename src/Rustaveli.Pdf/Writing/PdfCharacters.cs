namespace Rustaveli.Pdf.Writing;

/// <summary>The character classes of PDF's lexical conventions (ISO 32000-1, 7.2.2).</summary>
internal static class PdfCharacters
{
    public static bool IsWhitespace(byte value) =>
        value is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

    public static bool IsDelimiter(byte value) =>
        value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']'
            or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    /// <summary>
    /// True for bytes that continue a token: two regular characters side by side read as one token, so the writer
    /// separates tokens only where both neighbours are regular.
    /// </summary>
    public static bool IsRegular(byte value) => !IsWhitespace(value) && !IsDelimiter(value);

    /// <summary>
    /// True for bytes a name may contain unescaped: printable ASCII other than delimiters and the number sign that
    /// introduces an escape.
    /// </summary>
    public static bool IsNameCharacter(byte value) =>
        value is >= 0x21 and <= 0x7E && value != (byte)'#' && !IsDelimiter(value);

    /// <summary>The uppercase hexadecimal digit for the low four bits of <paramref name="value"/>.</summary>
    public static byte HexDigit(int value)
    {
        int nibble = value & 0xF;
        return (byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
    }
}
