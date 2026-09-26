namespace Rustaveli.Pdf.Writing;

/// <summary>A PDF string object: a sequence of bytes and the syntax to write them in. Immutable.</summary>
internal sealed class PdfString
{
    private readonly byte[] _bytes;

    /// <summary>A string holding a copy of <paramref name="bytes"/>.</summary>
    public PdfString(ReadOnlySpan<byte> bytes, PdfStringForm form = PdfStringForm.Literal)
        : this(bytes.ToArray(), form)
    {
    }

    private PdfString(byte[] bytes, PdfStringForm form)
    {
        _bytes = bytes;
        Form = form;
    }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public PdfStringForm Form { get; }

    /// <summary>
    /// A text string (ISO 32000-1, 7.9.2.2): PDFDocEncoding, one byte per character, when every character has a code
    /// there; otherwise UTF-16BE behind a byte order mark, written in hex because that is shorter than escaping the
    /// many zero bytes of UTF-16 text.
    /// </summary>
    public static PdfString FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[] bytes = new byte[text.Length];
        for (int index = 0; index < text.Length; index++)
        {
            int code = PdfDocEncoding.Encode(text[index]);
            if (code < 0)
                return FromUtf16(text);

            bytes[index] = (byte)code;
        }

        // "þÿ" encodes to FE FF, which every reader takes for a UTF-16 byte order mark, and "ï»¿" to the UTF-8
        // mark PDF 2.0 readers honour. Text that happens to begin that way has to be written as UTF-16 to read back
        // as itself.
        ReadOnlySpan<byte> encoded = bytes;
        if (encoded.StartsWith(Utf16ByteOrderMark) || encoded.StartsWith(Utf8ByteOrderMark))
            return FromUtf16(text);

        return new PdfString(bytes, PdfStringForm.Literal);
    }

    private static ReadOnlySpan<byte> Utf16ByteOrderMark => [0xFE, 0xFF];

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    private static PdfString FromUtf16(string text)
    {
        byte[] bytes = new byte[2 + (2 * text.Length)];
        bytes[0] = 0xFE;
        bytes[1] = 0xFF;
        for (int index = 0; index < text.Length; index++)
        {
            bytes[2 + (2 * index)] = (byte)(text[index] >> 8);
            bytes[3 + (2 * index)] = (byte)text[index];
        }

        return new PdfString(bytes, PdfStringForm.Hex);
    }
}
