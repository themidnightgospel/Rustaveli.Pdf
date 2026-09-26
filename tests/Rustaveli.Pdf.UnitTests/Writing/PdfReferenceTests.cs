using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfReferenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefusesObjectNumbersBelowOne(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfReference(number));
    }

    [Fact]
    public void AcceptsObjectNumberOne()
    {
        Assert.Equal(1, new PdfReference(1).ObjectNumber);
    }

    [Fact]
    public void EqualsReferencesToTheSameObject()
    {
        PdfReference reference = new PdfReference(12);

        Assert.True(reference == new PdfReference(12));
        Assert.False(reference != new PdfReference(12));
        Assert.True(reference.Equals((object)new PdfReference(12)));
        Assert.Equal(12, reference.GetHashCode());
    }

    [Fact]
    public void DiffersFromReferencesToOtherObjects()
    {
        PdfReference reference = new PdfReference(12);

        Assert.False(reference == new PdfReference(13));
        Assert.True(reference != new PdfReference(13));
        Assert.False(reference.Equals((object)12));
        Assert.False(reference.Equals(null));
    }

    [Fact]
    public void FormatsAsPdfSyntax()
    {
        Assert.Equal("12 0 R", new PdfReference(12).ToString());
    }
}
