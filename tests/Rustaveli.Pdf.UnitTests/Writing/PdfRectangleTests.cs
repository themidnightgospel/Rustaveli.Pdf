using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfRectangleTests
{
    [Fact]
    public void KeepsItsEdges()
    {
        PdfRectangle rectangle = new PdfRectangle(1, 2, 3, 4);

        Assert.Equal(1, rectangle.Left);
        Assert.Equal(2, rectangle.Bottom);
        Assert.Equal(3, rectangle.Right);
        Assert.Equal(4, rectangle.Top);
    }

    [Fact]
    public void WritesAsLeftBottomRightTop()
    {
        PdfRectangle rectangle = new PdfRectangle(0, -10.5, 595.28, 841.89);

        Assert.Equal("[0 -10.5 595.28 841.89]", Latin1.Written(writer => writer.WriteArray(rectangle.ToArray())));
    }

    [Theory]
    [InlineData(0.0, 2, 3, 4, true)]
    [InlineData(-0.0, 2, 3, 4, false)]
    [InlineData(0.0, 9, 3, 4, false)]
    [InlineData(0.0, 2, 9, 4, false)]
    [InlineData(0.0, 2, 3, 9, false)]
    public void IsIdenticalOnlyToTheSameFourNumbersToTheBit(
        double left, double bottom, double right, double top, bool identical)
    {
        // -0 equals 0, but its bits differ.
        Assert.Equal(identical, new PdfRectangle(0, 2, 3, 4).IsIdenticalTo(new PdfRectangle(left, bottom, right, top)));
    }
}
