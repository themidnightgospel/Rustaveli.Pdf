using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using CsCheck;
using Rustaveli.Pdf.UnitTests.Writing;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// What the writer produces must parse back to what it was given — numbers within the written precision, names,
/// strings and whole object graphs exactly, and complete files through their cross-reference sections — for any
/// input, not just the cases someone thought to write down.
/// </summary>
public class WriterPropertyTests
{
    private const long Iterations = 500;

    private static readonly Regex FixedPoint = new Regex(@"^-?(0|[1-9][0-9]*)(\.[0-9]{0,4}[1-9])?$");

    // At most five decimals, and seven significant digits from 100 upwards: half a unit in the last written place.
    private static double Tolerance(double value) => Math.Max(5e-6, Math.Abs(value) * 5e-7) * (1 + 1e-9);

    private static string WriteReal(double value)
    {
        byte[] buffer = new byte[PdfNumbers.MaxLength];
        int length = PdfNumbers.WriteReal(value, buffer);
        return Encoding.ASCII.GetString(buffer, 0, length);
    }

    private static void AssertEquivalent(PdfValue expected, object? actual)
    {
        switch (expected.Kind)
        {
            case PdfValueKind.Null:
                Assert.Null(actual);
                break;
            case PdfValueKind.Boolean:
                Assert.Equal(expected.AsBoolean(), actual);
                break;
            case PdfValueKind.Integer:
                Assert.Equal(expected.AsInteger(), actual);
                break;
            case PdfValueKind.Real:
                double parsed = actual is long integral ? integral : (double)actual!;
                double value = expected.AsReal();
                Assert.InRange(parsed - value, -Tolerance(value), Tolerance(value));
                break;
            case PdfValueKind.Name:
                Assert.Equal(new ParsedName(expected.AsName().Value), actual);
                break;
            case PdfValueKind.String:
                Assert.Equal(expected.AsString().Bytes.ToArray(), actual);
                break;
            case PdfValueKind.Array:
                List<object?> items = Assert.IsType<List<object?>>(actual);
                PdfArray array = expected.AsArray();
                Assert.Equal(array.Count, items.Count);
                for (int index = 0; index < array.Count; index++)
                    AssertEquivalent(array[index], items[index]);

                break;
            case PdfValueKind.Dictionary:
                Dictionary<string, object?> entries = Assert.IsType<Dictionary<string, object?>>(actual);
                PdfDictionary dictionary = expected.AsDictionary();
                Assert.Equal(dictionary.Count, entries.Count);
                foreach (KeyValuePair<PdfName, PdfValue> entry in dictionary)
                    AssertEquivalent(entry.Value, entries[entry.Key.Value]);

                break;
            default:
                Assert.Equal(new ParsedReference(expected.AsReference().ObjectNumber, 0), actual);
                break;
        }
    }

    [Fact]
    public void RealsAreWrittenInPlainFixedPointAndReadBackWithinPrecision()
    {
        WriterGenerators.Real.Sample(value =>
        {
            string written = WriteReal(value);

            Assert.Matches(FixedPoint, written);
            Assert.NotEqual("-0", written);
            double parsed = double.Parse(written, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(parsed - value, -Tolerance(value), Tolerance(value));
        }, iter: 5000);
    }

    [Fact]
    public void WritingARealTwiceGivesTheSameText()
    {
        WriterGenerators.Real.Sample(value =>
        {
            string written = WriteReal(value);

            Assert.Equal(written, WriteReal(double.Parse(written, System.Globalization.CultureInfo.InvariantCulture)));
        }, iter: 2000);
    }

    [Fact]
    public void NamesParseBackToTheirValue()
    {
        WriterGenerators.Text(40).Sample(value =>
        {
            PdfName name = new PdfName(value);
            byte[] encoded = name.Encoded.ToArray();

            Assert.All(encoded.Skip(1), code => Assert.InRange(code, (byte)0x21, (byte)0x7E));
            Assert.Equal(new ParsedName(value), PdfSyntaxParser.ParseSingle(encoded));
        }, iter: Iterations);
    }

    [Fact]
    public void StringsParseBackToTheirBytesInEitherForm()
    {
        Gen.Select(Gen.Byte.Array[0, 200], Gen.Bool).Sample((bytes, hex) =>
        {
            PdfString text = new PdfString(bytes, hex ? PdfStringForm.Hex : PdfStringForm.Literal);

            string written = Latin1.Written(writer => writer.WriteString(text));

            Assert.Equal(bytes, PdfSyntaxParser.ParseSingle(Latin1.Bytes(written)));
            Assert.DoesNotContain('\r', written);
            if (!hex)
                Assert.Equal(PdfByteWriter.LiteralLength(bytes), written.Length);
        }, iter: Iterations);
    }

    [Fact]
    public void TextStringsDecodeBackToTheirText()
    {
        Dictionary<int, char> pdfDocDecoding = new Dictionary<int, char>();
        for (int character = 0; character <= 0xFFFF; character++)
        {
            int code = PdfDocEncoding.Encode((char)character);
            if (code >= 0)
                pdfDocDecoding.Add(code, (char)character);
        }

        WriterGenerators.Text(30).Sample(value =>
        {
            byte[] bytes = PdfString.FromText(value).Bytes.ToArray();

            string decoded = bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
                ? Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)
                : new string(bytes.Select(code => pdfDocDecoding[code]).ToArray());

            Assert.Equal(value, decoded);
        }, iter: Iterations);
    }

    [Fact]
    public void ObjectGraphsParseBackToWhatWasWritten()
    {
        WriterGenerators.Value.Sample(value =>
        {
            string written = Latin1.Written(writer => writer.WriteValue(value));

            AssertEquivalent(value, PdfSyntaxParser.ParseSingle(Latin1.Bytes(written)));
        }, iter: Iterations);
    }

    [Fact]
    public void FilesReadBackObjectByObjectThroughTheirCrossReferenceSection()
    {
        Gen<(PdfValue Value, byte[]? Stream)> item = Gen.Frequency(
            (3, WriterGenerators.Value.Select(value => (value, (byte[]?)null))),
            (1, Gen.Byte.Array[0, 3000].Select(data => (PdfValue.Null, (byte[]?)data))));

        Gen.Select(item.List[1, 40], Gen.Bool, Gen.Bool).Sample((items, table, compress) =>
        {
            PdfWriterOptions options = new PdfWriterOptions
            {
                CrossReferenceFormat = table ? PdfCrossReferenceFormat.Table : PdfCrossReferenceFormat.Stream,
                CompressionLevel = compress ? CompressionLevel.Fastest : CompressionLevel.NoCompression,
            };

            List<(int Number, PdfValue Value, byte[]? Stream)> written = new List<(int, PdfValue, byte[]?)>();
            using MemoryStream output = new MemoryStream();
            using (PdfFileWriter writer = new PdfFileWriter(output, options))
            {
                foreach ((PdfValue value, byte[]? stream) in items)
                {
                    // A bare reference is not an object in its own right, so it travels inside an array.
                    PdfValue storable = value.Kind == PdfValueKind.Reference ? new PdfArray { value } : value;
                    PdfReference reference = stream == null
                        ? writer.Write(storable)
                        : writer.WriteStream(new PdfDictionary(), stream);
                    written.Add((reference.ObjectNumber, storable, stream));
                }

                writer.Finish(new PdfReference(written[0].Number));
            }

            PdfFileReader reader = new PdfFileReader(output.ToArray());
            foreach ((int number, PdfValue value, byte[]? stream) in written)
            {
                if (stream == null)
                    AssertEquivalent(value, reader.GetObject(number));
                else
                    Assert.Equal(stream, PdfFileReader.Decode(Assert.IsType<ParsedStream>(reader.GetObject(number))));
            }
        }, iter: 200);
    }
}
