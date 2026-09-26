using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>
/// One character per byte, both ways. PDF syntax is bytes, but expected output reads best as a string literal, and
/// Latin-1 is the one encoding that maps every byte to a character and back unchanged.
/// </summary>
internal static class Latin1
{
    public static string Text(ReadOnlySpan<byte> bytes)
    {
        char[] characters = new char[bytes.Length];
        for (int index = 0; index < bytes.Length; index++)
            characters[index] = (char)bytes[index];

        return new string(characters);
    }

    public static byte[] Bytes(string text) => text.Select(character => checked((byte)character)).ToArray();

    /// <summary>What <paramref name="write"/> puts into a fresh <see cref="PdfByteWriter"/>.</summary>
    public static string Written(Action<PdfByteWriter> write)
    {
        using PdfByteWriter writer = new PdfByteWriter();
        write(writer);
        return Text(writer.WrittenSpan);
    }
}
