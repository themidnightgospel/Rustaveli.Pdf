using System.IO.Compression;
using System.Text;
using Rustaveli.Pdf.Operations.Linearization;
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

    [Theory]
    [InlineData("DCTDecode")]
    [InlineData("DCT")]
    [InlineData("JPXDecode")]
    [InlineData("CCITTFaxDecode")]
    [InlineData("CCF")]
    [InlineData("JBIG2Decode")]
    public void NeverDecodesImageCodecs(string codec) =>
        Assert.Throws<NotSupportedException>(() => Decode(new PdfName(codec), [0xFF, 0xD8]));

    [Theory]
    [InlineData("Flate")]
    [InlineData("LZX")]
    [InlineData("AHX")]
    [InlineData("A86")]
    [InlineData("RL2")]
    [InlineData("RunLengthDecoder")]
    [InlineData("crypt")]
    public void RefusesFiltersItDoesNotKnowHoweverCloseTheirNames(string filter) =>
        Assert.Throws<NotSupportedException>(() => Decode(new PdfName(filter), Text("data")));

    [Fact]
    public void AnIdentityCryptFilterPassesTheDataThrough()
    {
        byte[] data = Text("clear");

        Assert.Equal(data, Decode(new PdfName("Crypt"), data));
        Assert.Equal(data, Decode(new PdfName("Crypt"), data, new PdfDictionary { [new PdfName("Name")] = new PdfName("Identity") }));
        Assert.Equal(data, Decode(new PdfName("Crypt"), data, new PdfDictionary { [PdfNames.Type] = new PdfName("CryptFilterDecodeParms") }));
        Assert.Throws<NotSupportedException>(() => Decode(new PdfName("Crypt"), data, new PdfDictionary { [new PdfName("Name")] = new PdfName("StdCF") }));
    }

    /// <summary><paramref name="text"/> encoded with the filter named <paramref name="filter"/>, in full or abbreviated.</summary>
    private static byte[] Encoded(string filter, string text) => filter switch
    {
        "FlateDecode" or "Fl" => Zlib(Text(text)),
        "LZWDecode" or "LZW" => Lzw(Text(text), earlyChange: true),
        "ASCIIHexDecode" or "AHx" => Text(BitConverter.ToString(Text(text)).Replace("-", string.Empty) + ">"),
        "ASCII85Decode" or "A85" => Text(Ascii85(Text(text))),
        _ => [(byte)(text.Length - 1), .. Text(text), 128],
    };

    [Theory]
    [InlineData("Fl", "FlateDecode")]
    [InlineData("LZW", "LZWDecode")]
    [InlineData("AHx", "ASCIIHexDecode")]
    [InlineData("A85", "ASCII85Decode")]
    [InlineData("RL", "RunLengthDecode")]
    public void AnAbbreviatedFilterNameDecodesAsTheFullName(string abbreviation, string name)
    {
        const string text = "abbreviated, as inline images name their filters";

        Assert.Equal(Text(text), Decode(new PdfName(name), Encoded(name, text)));
        Assert.Equal(Text(text), Decode(new PdfName(abbreviation), Encoded(abbreviation, text)));
    }

    [Fact]
    public void ParametersInAnArrayGoWithTheFilterInTheSamePlace()
    {
        byte[] rows = [0, 10, 20, 30, 2, 1, 1, 1];
        byte[] hexOfZlib = Text(BitConverter.ToString(Zlib(rows)).Replace("-", string.Empty) + ">");
        PdfArray filters = new PdfArray { new PdfName("AHx"), new PdfName("FlateDecode") };
        PdfArray parameters = new PdfArray { PdfValue.Null, new PdfDictionary { [Predictor] = 12, [Columns] = 3 } };

        Assert.Equal<byte>([10, 20, 30, 11, 21, 31], Decode(filters, hexOfZlib, parameters));
    }

    [Fact]
    public void AParameterThatIsNotAnIntegerIsTakenAsUnset()
    {
        byte[] rows = [2, 1, 1, 1];
        PdfDictionary parameters = new PdfDictionary { [Predictor] = new PdfName("PNG"), [Columns] = 3 };

        Assert.Equal(rows, Decode(new PdfName("FlateDecode"), Zlib(rows), parameters));
    }

    [Fact]
    public void AnEmptyStreamInflatesToNothing() => Assert.Empty(StreamDecoder.Inflate([]));

    [Fact]
    public void RawDeflateWhoseFirstBytesOnlyLookLikeAZlibHeaderIsInflatedWhole()
    {
        // A stored block that is not the last, then an empty last one. 0x08 has the low nibble of zlib's deflate method,
        // but 0x0805 is no multiple of 31, so neither byte is taken for a header.
        byte[] raw = [0x08, 0x05, 0x00, 0xFA, 0xFF, .. Text("hello"), 0x01, 0x00, 0x00, 0xFF, 0xFF];

        Assert.Equal(Text("hello"), StreamDecoder.Inflate(raw));
    }

    [Fact]
    public void ThePaethPredictorTakesWhicheverNeighbourIsNearestItsEstimate()
    {
        // The second row's three bytes are predicted from above, from the left, and from above-left, in that order.
        byte[] rows =
        [
            0, 5, 5, 10,
            4, 0, 251, 1,
        ];

        PdfDictionary parameters = new PdfDictionary { [Predictor] = 14, [Columns] = 3 };

        Assert.Equal<byte>([5, 5, 10, 5, 0, 6], Decode(new PdfName("FlateDecode"), Zlib(rows), parameters));
    }

    /// <summary>
    /// LZW as the specification encodes it, with codes widening as the decoder's table grows and the table cleared
    /// when full, to check decoding against.
    /// </summary>
    private static byte[] Lzw(byte[] data, bool earlyChange, bool endOfData = true)
    {
        BitWriter output = new BitWriter();
        Dictionary<string, int> codes = [];
        int width = 9;

        // The decoder's table, which grows one code behind the encoder's: by one for each code but the first.
        int decoded = 258;
        bool first = true;

        void Start()
        {
            codes.Clear();
            for (int code = 0; code < 256; code++)
                codes[((char)code).ToString()] = code;

            width = 9;
            decoded = 258;
            first = true;
        }

        void Emit(int code)
        {
            output.Write(code, width);

            if (first)
                first = false;
            else
                decoded++;

            if (decoded + (earlyChange ? 1 : 0) >= (1 << width) && width < 12)
                width++;
        }

        Start();
        output.Write(256, width);
        string word = string.Empty;

        foreach (byte value in data)
        {
            string longer = word + (char)value;

            if (codes.ContainsKey(longer))
            {
                word = longer;
                continue;
            }

            Emit(codes[word]);
            codes[longer] = codes.Count + 2;
            word = ((char)value).ToString();

            if (codes.Count + 2 == 4096)
            {
                output.Write(256, width);
                Start();
            }
        }

        if (word.Length > 0)
            Emit(codes[word]);

        if (endOfData)
            output.Write(257, width);

        return output.ToArray();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DecodesLzwWideningItsCodesAndClearingItsTableWhenFull(bool earlyChange)
    {
        // Random bytes repeat little, so they fill the table, through every code width, more than once.
        byte[] original = new byte[20000];
        new Random(11).NextBytes(original);
        PdfDictionary parameters = new PdfDictionary { [new PdfName("EarlyChange")] = earlyChange ? 1 : 0 };

        Assert.Equal(original, Decode(new PdfName("LZWDecode"), Lzw(original, earlyChange), parameters));
    }

    [Fact]
    public void AnLzwStreamWithoutItsEndCodeEndsWithItsData()
    {
        byte[] original = Text(string.Concat(Enumerable.Repeat("no end code ", 40)));

        Assert.Equal(original, Decode(new PdfName("LZWDecode"), Lzw(original, earlyChange: true, endOfData: false)));
    }

    [Fact]
    public void AnLzwCodeNoTableHoldsYetEndsTheStream()
    {
        // "A", a clear, then code 300 where only 258 codes exist and there is no previous string to extend.
        BitWriter encoded = new BitWriter();
        encoded.Write(65, 9);
        encoded.Write(256, 9);
        encoded.Write(300, 9);
        encoded.Write(66, 9);

        Assert.Equal(Text("A"), Decode(new PdfName("LZWDecode"), encoded.ToArray()));
    }

    [Fact]
    public void Ascii85SkipsWhatIsNotADigitOfIt()
    {
        // Past 'u', below '!' without being whitespace, and a 'z' inside a group, where it cannot stand for zeros.
        string encoded = Ascii85(Text("Hello Wo")).Insert(2, "v{\u0001z");

        Assert.Equal(Text("Hello Wo"), Decode(new PdfName("ASCII85Decode"), Text(encoded)));
    }

    [Fact]
    public void ARunCutShortOfTheByteItRepeatsRepeatsNothing()
    {
        byte[] encoded = [2, (byte)'a', (byte)'b', (byte)'c', 254];

        Assert.Equal(Text("abc"), Decode(new PdfName("RunLengthDecode"), encoded));
    }
}
