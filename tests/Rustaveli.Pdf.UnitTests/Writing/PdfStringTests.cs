using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfStringTests
{
    [Fact]
    public void EncodesAsciiTextOneBytePerCharacterAsALiteral()
    {
        PdfString text = PdfString.FromText("Hello");

        Assert.Equal(PdfStringForm.Literal, text.Form);
        Assert.Equal(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }, text.Bytes.ToArray());
    }

    [Fact]
    public void EncodesWesternPunctuationInPdfDocEncoding()
    {
        PdfString text = PdfString.FromText("Café — “naïve” €");

        Assert.Equal(PdfStringForm.Literal, text.Form);
        Assert.Equal(
            new byte[] { 0x43, 0x61, 0x66, 0xE9, 0x20, 0x84, 0x20, 0x8D, 0x6E, 0x61, 0xEF, 0x76, 0x65, 0x8E, 0x20, 0xA0 },
            text.Bytes.ToArray());
    }

    [Fact]
    public void FallsBackToUtf16WithAByteOrderMarkForOtherScripts()
    {
        PdfString text = PdfString.FromText("აბ c");

        Assert.Equal(PdfStringForm.Hex, text.Form);
        Assert.Equal(new byte[] { 0xFE, 0xFF, 0x10, 0xD0, 0x10, 0xD1, 0x00, 0x20, 0x00, 0x63 }, text.Bytes.ToArray());
    }

    [Fact]
    public void KeepsSurrogatePairsAsTwoCodeUnits()
    {
        PdfString text = PdfString.FromText("\U0001D11E");

        Assert.Equal(new byte[] { 0xFE, 0xFF, 0xD8, 0x34, 0xDD, 0x1E }, text.Bytes.ToArray());
    }

    [Fact]
    public void EncodesEmptyTextAsAnEmptyLiteral()
    {
        PdfString text = PdfString.FromText(string.Empty);

        Assert.Equal(PdfStringForm.Literal, text.Form);
        Assert.Equal(0, text.Bytes.Length);
    }

    [Theory]
    [InlineData("þÿx", new byte[] { 0xFE, 0xFF, 0x00, 0xFE, 0x00, 0xFF, 0x00, 0x78 })]
    [InlineData("ï»¿", new byte[] { 0xFE, 0xFF, 0x00, 0xEF, 0x00, 0xBB, 0x00, 0xBF })]
    public void AvoidsPdfDocEncodingThatWouldReadAsAByteOrderMark(string value, byte[] expected)
    {
        PdfString text = PdfString.FromText(value);

        Assert.Equal(PdfStringForm.Hex, text.Form);
        Assert.Equal(expected, text.Bytes.ToArray());
    }

    [Theory]
    [InlineData("þx", new byte[] { 0xFE, 0x78 })]
    [InlineData("ÿþ", new byte[] { 0xFF, 0xFE })]
    [InlineData("ï»x", new byte[] { 0xEF, 0xBB, 0x78 })]
    [InlineData("ï»", new byte[] { 0xEF, 0xBB })]
    public void KeepsPdfDocEncodingForTextThatOnlyResemblesAByteOrderMark(string value, byte[] expected)
    {
        PdfString text = PdfString.FromText(value);

        Assert.Equal(PdfStringForm.Literal, text.Form);
        Assert.Equal(expected, text.Bytes.ToArray());
    }

    [Fact]
    public void RejectsNullText()
    {
        Assert.Throws<ArgumentNullException>(() => PdfString.FromText(null!));
    }

    [Fact]
    public void CopiesTheBytesItIsGiven()
    {
        byte[] bytes = { 1, 2, 3 };
        PdfString text = new PdfString(bytes);

        bytes[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, text.Bytes.ToArray());
    }

    [Fact]
    public void DefaultsToTheLiteralFormAndKeepsAnExplicitOne()
    {
        Assert.Equal(PdfStringForm.Literal, new PdfString(new byte[] { 1 }).Form);
        Assert.Equal(PdfStringForm.Hex, new PdfString(new byte[] { 1 }, PdfStringForm.Hex).Form);
    }
}
