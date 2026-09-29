using System.Text;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>
/// Reading PDF objects from bytes, into values the writer writes back exactly as they were read.
/// </summary>
public class PdfParserTests
{
    private static PdfValue Read(string text) => new PdfParser(Encoding.Latin1.GetBytes(text)).ReadValue();

    /// <summary>A value as the writer writes it.</summary>
    private static string Written(PdfValue value)
    {
        using PdfByteWriter writer = new PdfByteWriter();
        writer.WriteValue(value);
        return Encoding.Latin1.GetString(writer.WrittenSpan);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ReadsBooleans(string text, bool expected) => Assert.Equal(expected, Read(text).AsBoolean());

    [Fact]
    public void ReadsNull() => Assert.Equal(PdfValueKind.Null, Read("null").Kind);

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("42", 42L)]
    [InlineData("-17", -17L)]
    [InlineData("+5", 5L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void ReadsIntegers(string text, long expected) => Assert.Equal(expected, Read(text).AsInteger());

    [Theory]
    [InlineData("3.25", 3.25)]
    [InlineData("-.5", -0.5)]
    [InlineData("4.", 4.0)]
    [InlineData("0.000488281", 0.000488281)]
    [InlineData("99999999999999999999", 1e20)]
    public void ReadsRealsAndWritesThemBackAsTheyWere(string text, double expected)
    {
        PdfValue value = Read(text);

        Assert.Equal(expected, value.AsReal(), 12);
        Assert.Equal(text, Written(value));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("1-2")]
    [InlineData("1.2.3x")]
    [InlineData("403.1.7")]
    [InlineData("1..2")]
    [InlineData("obj")]
    public void RefusesWhatIsNeitherANumberNorAValue(string text) => Assert.Throws<UnreadableFileException>(() => Read(text));

    [Theory]
    [InlineData("/Type", "Type", "/Type")]
    [InlineData("/A#20B", "A B", "/A#20B")]
    [InlineData("/Lime#20Green", "Lime Green", "/Lime#20Green")]
    [InlineData("/", "", "/")]
    [InlineData("/Half#4", "Half#4", "/Half#234")]
    [InlineData("/Cut#00Off", "Cut", "/Cut")]
    public void ReadsNamesUndoingTheirEscapes(string text, string value, string written)
    {
        PdfName name = Read(text).AsName();

        Assert.Equal(value, name.Value);
        Assert.Equal(written, Written(name));
    }

    [Fact]
    public void ANameThatIsNotUtf8KeepsItsBytes()
    {
        PdfName name = Read("/Caf#E9").AsName();

        Assert.Equal("Café", name.Value);
        Assert.Equal("/Caf#E9", Written(name));
    }

    [Fact]
    public void AUtf8NameIsTheNameItsTextMakes() =>
        Assert.Equal(new PdfName("Café"), Read("/Caf#C3#A9").AsName());

    [Theory]
    [InlineData("(Hello)", "Hello")]
    [InlineData("(a (nested) pair)", "a (nested) pair")]
    [InlineData(@"(line\nfeed\r\t\b\f)", "line\nfeed\r\t\b\f")]
    [InlineData(@"(\(escaped\) \\ slash)", @"(escaped) \ slash")]
    [InlineData(@"(\101\60\7x)", "A0\u0007x")]
    [InlineData("(joined\\\r\nline)", "joinedline")]
    [InlineData("(joined\\\nline)", "joinedline")]
    [InlineData("(bare\r\nbreak)", "bare\nbreak")]
    [InlineData("(bare\rbreak)", "bare\nbreak")]
    [InlineData(@"(needless\q)", "needlessq")]
    public void ReadsLiteralStrings(string text, string expected)
    {
        PdfString value = Read(text).AsString();

        Assert.Equal(expected, Encoding.Latin1.GetString(value.Bytes.ToArray()));
        Assert.Equal(PdfStringForm.Literal, value.Form);
    }

    [Theory]
    [InlineData("<48656C6C6F>", "Hello")]
    [InlineData("<48 65\n6c 6C 6f>", "Hello")]
    [InlineData("<414>", "A@")]
    [InlineData("<>", "")]
    public void ReadsHexadecimalStrings(string text, string expected)
    {
        PdfString value = Read(text).AsString();

        Assert.Equal(expected, Encoding.Latin1.GetString(value.Bytes.ToArray()));
        Assert.Equal(PdfStringForm.Hex, value.Form);
    }

    [Theory]
    [InlineData("(unended")]
    [InlineData("<4142")]
    [InlineData("<41G2>")]
    [InlineData("[1 2")]
    [InlineData("<</A 1")]
    [InlineData("<</A 1>")]
    [InlineData("<<1 2>>")]
    [InlineData("")]
    [InlineData("   % only a comment")]
    [InlineData(")")]
    public void RefusesDamagedObjects(string text) => Assert.Throws<UnreadableFileException>(() => Read(text));

    [Fact]
    public void ReadsArraysOfMixedValues()
    {
        PdfArray array = Read("[1 2.5/N(s)<41>[true]<</K null>>]").AsArray();

        Assert.Equal(7, array.Count);
        Assert.Equal(1L, array[0].AsInteger());
        Assert.Equal(2.5, array[1].AsReal());
        Assert.Equal("N", array[2].AsName().Value);
        Assert.True(array[5].AsArray()[0].AsBoolean());
        Assert.Empty(array[6].AsDictionary());
    }

    [Fact]
    public void ReadsReferencesAndTellsThemFromIntegers()
    {
        PdfArray array = Read("[12 0 R 7 8 9 0 R 3]").AsArray();

        Assert.Equal(5, array.Count);
        Assert.Equal(new PdfReference(12), array[0].AsReference());
        Assert.Equal(7L, array[1].AsInteger());
        Assert.Equal(8L, array[2].AsInteger());
        Assert.Equal(new PdfReference(9), array[3].AsReference());
        Assert.Equal(3L, array[4].AsInteger());
    }

    [Fact]
    public void AReferenceToObjectZeroIsNull() => Assert.Equal(PdfValueKind.Null, Read("0 0 R").Kind);

    [Fact]
    public void AnRNotStandingAloneIsNotAReference()
    {
        PdfParser parser = new PdfParser(Encoding.Latin1.GetBytes("5 0 RG"));

        Assert.Equal(5L, parser.ReadValue().AsInteger());
        Assert.Equal(0L, parser.ReadValue().AsInteger());
    }

    [Fact]
    public void ReadsDictionariesDroppingNullEntries()
    {
        PdfDictionary dictionary = Read("<< /Type /Page /Gone null % a comment\n /Count 3 >>").AsDictionary();

        Assert.Equal(2, dictionary.Count);
        Assert.Equal("Page", dictionary[PdfNames.Type].AsName().Value);
        Assert.Equal(3L, dictionary[PdfNames.Count].AsInteger());
    }

    [Fact]
    public void RefusesNestingNoRealFileHas() =>
        Assert.Throws<UnreadableFileException>(() => Read(new string('[', 300)));

    [Fact]
    public void KeywordsAndIntegersAreReadOnlyWhenTheyComeNext()
    {
        PdfParser parser = new PdfParser(Encoding.Latin1.GetBytes("  12 0 obj<<>>endobj"));

        Assert.False(parser.TryReadKeyword("obj"u8));
        Assert.True(parser.TryReadInteger(out long number));
        Assert.True(parser.TryReadInteger(out long generation));
        Assert.False(parser.TryReadInteger(out _));
        Assert.True(parser.TryReadKeyword("obj"u8));
        Assert.Equal((12L, 0L), (number, generation));
        Assert.Empty(parser.ReadValue().AsDictionary());
        Assert.True(parser.TryReadKeyword("endobj"u8));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(9, true)]
    [InlineData(32, true)]
    [InlineData((int)'(', false)]
    [InlineData((int)'a', false)]
    public void KnowsWhitespace(int value, bool whitespace) => Assert.Equal(whitespace, PdfParser.IsWhitespace((byte)value));

    [Theory]
    [InlineData('(')]
    [InlineData(')')]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData('[')]
    [InlineData(']')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('/')]
    [InlineData('%')]
    public void KnowsDelimiters(char value)
    {
        Assert.True(PdfParser.IsDelimiter((byte)value));
        Assert.False(PdfParser.IsRegular((byte)value));
    }
}
