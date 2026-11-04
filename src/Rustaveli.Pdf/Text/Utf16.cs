namespace Rustaveli.Pdf.Text;

/// <summary>Reads code points out of text held as UTF-16, as .NET strings hold it.</summary>
internal static class Utf16
{
    /// <summary>The last code point Unicode has, the last a surrogate pair can stand for.</summary>
    public const int MaxCodepoint = 0x10FFFF;

    /// <summary>
    /// The code point at <paramref name="index"/>, <paramref name="length"/> code units long: a surrogate pair read as
    /// one, a lone surrogate as itself.
    /// </summary>
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
}
