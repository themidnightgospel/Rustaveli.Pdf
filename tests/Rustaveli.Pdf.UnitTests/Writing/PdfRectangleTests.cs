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
}
