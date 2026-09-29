using System.IO.Compression;
using System.Text;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>
/// Undoing the filters streams are encoded with.
/// </summary>
public class StreamDecoderTests
{
    private static readonly PdfName Predictor = new PdfName("Predictor");
    private static readonly PdfName Colors = new PdfName("Colors");
    private static readonly PdfName Columns = new PdfName("Columns");
    private static readonly PdfName BitsPerComponent = new PdfName("BitsPerComponent");

    private static byte[] Decode(PdfValue filter, byte[] data, PdfValue parameters = default)
    {
        PdfDictionary dictionary = new PdfDictionary { [PdfNames.Filter] = filter };

        if (parameters.Kind != PdfValueKind.Null)
            dictionary[PdfNames.DecodeParms] = parameters;

        return StreamDecoder.Decode(dictionary, data, value => value);
    }

    private static byte[] Zlib(byte[] data)
    {
        using PdfByteWriter output = new PdfByteWriter();
        ZlibEncoder.Compress(data, CompressionLevel.Optimal, output);
        return output.WrittenSpan.ToArray();
    }

    private static byte[] Text(string text) => Encoding.Latin1.GetBytes(text);

    [Fact]
    public void AStreamWithoutFiltersIsItsData()
    {
        byte[] data = Text("plain");

        Assert.Same(data, StreamDecoder.Decode(new PdfDictionary(), data, value => value));
    }

    [Fact]
    public void InflatesZlibAndRawDeflate()
    {
        byte[] original = Text(string.Concat(Enumerable.Repeat("compress me ", 50)));
        byte[] zlib = Zlib(original);

        Assert.Equal(original, Decode(new PdfName("FlateDecode"), zlib));
        Assert.Equal(original, Decode(new PdfName("Fl"), zlib));
        Assert.Equal(original, StreamDecoder.Inflate(zlib.AsSpan(2, zlib.Length - 6).ToArray()));
    }

    [Fact]
    public void AStreamThatInflatesPastAnyPagesNeedIsUnreadable()
    {
        // Zeros deflate about a thousand to one: a third of a megabyte inflates to 300 MB, and a few more to past 2 GB.
        byte[] bomb;
        using (MemoryStream compressed = new MemoryStream())
        {
            using (DeflateStream deflater = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                byte[] zeros = new byte[1 << 20];

                for (int megabyte = 0; megabyte < 300; megabyte++)
                    deflater.Write(zeros, 0, zeros.Length);
            }

            bomb = compressed.ToArray();
        }

        UnreadableFileException exception = Assert.Throws<UnreadableFileException>(() => Decode(new PdfName("FlateDecode"), bomb));

        Assert.Contains("inflates", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStreamCutShortGivesWhatInflated()
    {
        byte[] original = new byte[5000];
        new Random(7).NextBytes(original);
        byte[] zlib = Zlib(original);

        byte[] partial = StreamDecoder.Inflate(zlib.AsSpan(0, zlib.Length / 2).ToArray());

        Assert.True(partial.Length < original.Length);
        Assert.Equal(original.Take(partial.Length), partial);
    }

    [Fact]
    public void UndoesPngPredictorsRowByRow()
    {
        // Two rows of three one-byte pixels: None, Sub, Up, Average and Paeth, one filter per row.
        byte[] rows =
        [
            0, 10, 20, 30,
            1, 5, 1, 1,
            2, 1, 1, 1,
            3, 10, 10, 10,
            4, 1, 2, 3,
        ];

        PdfDictionary parameters = new PdfDictionary { [Predictor] = 12, [Columns] = 3 };
        byte[] decoded = Decode(new PdfName("FlateDecode"), Zlib(rows), parameters);

        Assert.Equal<byte>([10, 20, 30, 5, 6, 7, 6, 7, 8, 13, 20, 24, 14, 22, 27], decoded);
    }

    [Fact]
    public void UndoesPngPredictorsForManyBytesAPixel()
    {
        byte[] rows = [1, 1, 2, 3, 4, 5, 6];
        PdfDictionary parameters = new PdfDictionary { [Predictor] = 15, [Colors] = 3, [Columns] = 2 };

        Assert.Equal<byte>([1, 2, 3, 5, 7, 9], Decode(new PdfName("FlateDecode"), Zlib(rows), parameters));
    }

    [Fact]
    public void UndoesTheTiffPredictor()
    {
        byte[] rows = [10, 20, 5, 5, 1, 2, 3, 4];
        PdfDictionary parameters = new PdfDictionary { [Predictor] = 2, [Colors] = 2, [Columns] = 2 };

        Assert.Equal<byte>([10, 20, 15, 25, 1, 2, 4, 6], Decode(new PdfName("FlateDecode"), Zlib(rows), parameters));
    }

    [Fact]
    public void TheTiffPredictorIsUndoneOnlyForWholeBytes()
    {
        PdfDictionary parameters = new PdfDictionary { [Predictor] = 2, [BitsPerComponent] = 4, [Columns] = 4 };

        Assert.Throws<NotSupportedException>(() => Decode(new PdfName("FlateDecode"), Zlib([1, 2]), parameters));
    }

    [Fact]
    public void DecodesTheSpecificationsLzwExample()
    {
        // ISO 32000-1, 7.4.4.2: "-----A---B" encodes as these codes, early change on.
        byte[] encoded = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];

        Assert.Equal(Text("-----A---B"), Decode(new PdfName("LZWDecode"), encoded));
    }

    [Theory]
    [InlineData("48656C6C6F>", "Hello")]
    [InlineData("48 65 6c\n6C 6f", "Hello")]
    [InlineData("414>ignored", "A@")]
    public void DecodesHexadecimal(string encoded, string expected) =>
        Assert.Equal(Text(expected), Decode(new PdfName("ASCIIHexDecode"), Text(encoded)));

    /// <summary>ASCII85 as the specification defines it, to check decoding against.</summary>
    private static string Ascii85(byte[] data)
    {
        StringBuilder text = new StringBuilder();

        for (int start = 0; start < data.Length; start += 4)
        {
            int count = Math.Min(4, data.Length - start);
            uint group = 0;

            for (int index = 0; index < 4; index++)
                group = (group << 8) | (index < count ? data[start + index] : 0u);

            char[] digits = new char[5];
            for (int index = 4; index >= 0; index--)
            {
                digits[index] = (char)('!' + (group % 85));
                group /= 85;
            }

            text.Append(digits, 0, count + 1);
        }

        return text.Append("~>").ToString();
    }

    [Theory]
    [InlineData("Hello World")]
    [InlineData("Hello Wo")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("")]
    public void DecodesAscii85(string text) =>
        Assert.Equal(Text(text), Decode(new PdfName("ASCII85Decode"), Text(Ascii85(Text(text)))));

    [Fact]
    public void Ascii85TakesZForFourZerosAndSkipsWhitespace()
    {
        string encoded = Ascii85(Text("Hello Wo"));

        Assert.Equal(Text("\0\0\0\0Hello Wo"), Decode(new PdfName("A85"), Text("z " + encoded.Insert(3, "\n"))));
    }

    [Fact]
    public void DecodesRunLengths()
    {
        byte[] encoded = [2, (byte)'a', (byte)'b', (byte)'c', 254, (byte)'x', 128, (byte)'z'];

        Assert.Equal(Text("abcxxx"), Decode(new PdfName("RunLengthDecode"), encoded));
    }

    [Fact]
    public void UndoesFiltersInTheOrderNamed()
    {
        byte[] hexOfZlib = Text(BitConverter.ToString(Zlib(Text("layered"))).Replace("-", string.Empty) + ">");
        PdfArray filters = new PdfArray { new PdfName("AHx"), new PdfName("FlateDecode") };

        Assert.Equal(Text("layered"), Decode(filters, hexOfZlib, new PdfArray { PdfValue.Null, PdfValue.Null }));
    }

    [Fact]
    public void NeverDecodesImageCodecs() =>
        Assert.Throws<NotSupportedException>(() => Decode(new PdfName("DCTDecode"), [0xFF, 0xD8]));

    [Fact]
    public void AnIdentityCryptFilterPassesTheDataThrough()
    {
        byte[] data = Text("clear");

        Assert.Equal(data, Decode(new PdfName("Crypt"), data));
        Assert.Equal(data, Decode(new PdfName("Crypt"), data, new PdfDictionary { [new PdfName("Name")] = new PdfName("Identity") }));
        Assert.Throws<NotSupportedException>(() => Decode(new PdfName("Crypt"), data, new PdfDictionary { [new PdfName("Name")] = new PdfName("StdCF") }));
    }
}
