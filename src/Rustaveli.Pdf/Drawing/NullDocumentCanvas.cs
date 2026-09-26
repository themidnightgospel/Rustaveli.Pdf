using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Discards every drawing operation.
/// </summary>
/// <remarks>
/// Used for the counting pass, where the engine needs to know how many pages a document produces before it can
/// resolve content such as "page 2 of 7". Running the identical layout against a canvas that draws nothing keeps
/// the two passes consistent without emitting output twice.
/// </remarks>
public sealed class NullDocumentCanvas : IDocumentCanvas
{
    public int PageCount { get; private set; }

    public void BeginPage(Size size) => PageCount++;

    public void EndPage()
    {
    }

    public void Save()
    {
    }

    public void Restore()
    {
    }

    public void Translate(Position offset)
    {
    }

    public void Scale(float scaleX, float scaleY)
    {
    }

    public void Rotate(float degrees)
    {
    }

    public void ClipRectangle(Size size)
    {
    }

    public void DrawRectangle(Position position, Size size, Ink color)
    {
    }

    public void DrawRoundedRectangle(Position position, Size size, float cornerRadius, Ink color, float strokeWidth = 0f)
    {
    }

    public void DrawLine(Position from, Position to, float thickness, Ink color)
    {
    }

    public void DrawText(string text, Position baselineStart, TextStyle style)
    {
    }

    public void DrawImage(IImage image, Size size)
    {
    }

    public void DrawExternalLink(string url, Size size)
    {
    }

    public void DrawInternalLink(string destinationName, Size size)
    {
    }

    public void DrawDestination(string destinationName)
    {
    }

    public void Dispose()
    {
    }
}
