using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfValueTests
{
    [Fact]
    public void DefaultsToNull()
    {
        Assert.Equal(PdfValueKind.Null, default(PdfValue).Kind);
        Assert.Equal(PdfValueKind.Null, PdfValue.Null.Kind);
    }

    [Fact]
    public void HoldsBooleans()
    {
        PdfValue yes = true;
        PdfValue no = false;

        Assert.Equal(PdfValueKind.Boolean, yes.Kind);
        Assert.True(yes.AsBoolean());
        Assert.False(no.AsBoolean());
    }

    [Fact]
    public void HoldsIntegersFromIntAndLong()
    {
        PdfValue small = -5;
        PdfValue large = 5_000_000_000L;

        Assert.Equal(PdfValueKind.Integer, small.Kind);
        Assert.Equal(-5, small.AsInteger());
        Assert.Equal(PdfValueKind.Integer, large.Kind);
        Assert.Equal(5_000_000_000L, large.AsInteger());
    }

    [Fact]
    public void HoldsRealsBitForBit()
    {
        PdfValue real = 0.1;
        PdfValue negativeZero = -0.0;
        PdfValue widened = 1.5f;

        Assert.Equal(PdfValueKind.Real, real.Kind);
        Assert.Equal(0.1, real.AsReal());
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(negativeZero.AsReal()));
        Assert.Equal(PdfValueKind.Real, widened.Kind);
        Assert.Equal(1.5, widened.AsReal());
    }

    [Fact]
    public void HoldsObjectsByReference()
    {
        PdfName name = new PdfName("N");
        PdfString text = new PdfString(new byte[] { 1 });
        PdfArray array = new PdfArray();
        PdfDictionary dictionary = new PdfDictionary();

        PdfValue nameValue = name;
        PdfValue textValue = text;
        PdfValue arrayValue = array;
        PdfValue dictionaryValue = dictionary;

        Assert.Equal(PdfValueKind.Name, nameValue.Kind);
        Assert.Same(name, nameValue.AsName());
        Assert.Equal(PdfValueKind.String, textValue.Kind);
        Assert.Same(text, textValue.AsString());
        Assert.Equal(PdfValueKind.Array, arrayValue.Kind);
        Assert.Same(array, arrayValue.AsArray());
        Assert.Equal(PdfValueKind.Dictionary, dictionaryValue.Kind);
        Assert.Same(dictionary, dictionaryValue.AsDictionary());
    }

    [Fact]
    public void HoldsReferences()
    {
        PdfValue value = new PdfReference(42);

        Assert.Equal(PdfValueKind.Reference, value.Kind);
        Assert.Equal(new PdfReference(42), value.AsReference());
    }

    [Fact]
    public void RefusesAReferenceThatWasNeverAssigned()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => (PdfValue)default(PdfReference));

        Assert.StartsWith("The reference was never assigned an object number.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesNullObjects()
    {
        Assert.Throws<ArgumentNullException>(() => (PdfValue)(PdfName)null!);
        Assert.Throws<ArgumentNullException>(() => (PdfValue)(PdfString)null!);
        Assert.Throws<ArgumentNullException>(() => (PdfValue)(PdfArray)null!);
        Assert.Throws<ArgumentNullException>(() => (PdfValue)(PdfDictionary)null!);
    }

    [Fact]
    public void ReportsBothKindsWhenReadAsTheWrongKind()
    {
        PdfValue value = 3;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => value.AsReal());

        Assert.Equal("The value is Integer, not Real.", exception.Message);
    }

    [Fact]
    public void RefusesEveryAccessorButItsOwn()
    {
        PdfValue nothing = PdfValue.Null;

        Assert.Throws<InvalidOperationException>(() => nothing.AsBoolean());
        Assert.Throws<InvalidOperationException>(() => nothing.AsInteger());
        Assert.Throws<InvalidOperationException>(() => nothing.AsReal());
        Assert.Throws<InvalidOperationException>(() => nothing.AsName());
        Assert.Throws<InvalidOperationException>(() => nothing.AsString());
        Assert.Throws<InvalidOperationException>(() => nothing.AsArray());
        Assert.Throws<InvalidOperationException>(() => nothing.AsDictionary());
        Assert.Throws<InvalidOperationException>(() => nothing.AsReference());
    }
}
