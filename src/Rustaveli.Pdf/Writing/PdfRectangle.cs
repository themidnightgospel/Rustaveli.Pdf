namespace Rustaveli.Pdf.Writing;

/// <summary>A rectangle in PDF user space, where Y grows upwards from the bottom-left corner of the page.</summary>
internal readonly struct PdfRectangle
{
    public PdfRectangle(double left, double bottom, double right, double top)
    {
        Left = left;
        Bottom = bottom;
        Right = right;
        Top = top;
    }

    public double Left { get; }

    public double Bottom { get; }

    public double Right { get; }

    public double Top { get; }

    /// <summary><c>[left bottom right top]</c>, the form of <c>/MediaBox</c> and <c>/Rect</c>.</summary>
    public PdfArray ToArray() => new PdfArray(4) { Left, Bottom, Right, Top };

    /// <summary>Whether <paramref name="other"/> holds the same four numbers to the bit, so is written alike.</summary>
    public bool IsIdenticalTo(PdfRectangle other) =>
        Same(Left, other.Left) && Same(Bottom, other.Bottom) && Same(Right, other.Right) && Same(Top, other.Top);

    private static bool Same(double first, double second) =>
        BitConverter.DoubleToInt64Bits(first) == BitConverter.DoubleToInt64Bits(second);
}
