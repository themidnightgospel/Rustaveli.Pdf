namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Discards every drawing operation.
/// </summary>
/// <remarks>
/// Used for the counting pass, where the engine needs to know how many pages a document produces before it can
/// resolve content such as "page 2 of 7". Running the identical layout against a sink that draws nothing keeps
/// the two passes consistent without emitting output twice.
/// </remarks>
internal sealed class CountingPageSink : IPageSink
{
    public int PageCount { get; private set; }

    public void BeginPage(Extent size) => PageCount++;

    public void EndPage()
    {
    }

    public void Save()
    {
    }

    public void Restore()
    {
    }

    public void Translate(Offset offset)
    {
    }

    public void Scale(float scaleX, float scaleY)
    {
    }

    public void Rotate(float degrees)
    {
    }

    public void ClipRectangle(Extent size)
    {
    }

    public void DrawRectangle(Offset position, Extent size, Ink color)
    {
    }

    public void DrawRoundedRectangle(Offset position, Extent size, float cornerRadius, Ink color, float strokeWidth = 0f)
    {
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid)
    {
    }

    public void DrawText(string text, Offset baselineStart, TypeStyle style)
    {
    }

    public void DrawImage(IImage image, Extent size)
    {
    }

    public void DrawExternalLink(string url, Extent size)
    {
    }

    public void DrawInternalLink(string destinationName, Extent size)
    {
    }

    public void DrawDestination(string destinationName)
    {
    }

    public void Dispose()
    {
    }
}
