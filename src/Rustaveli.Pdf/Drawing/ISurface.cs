using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// The drawing surface elements target. Coordinates are in PDF points with the origin at the top-left of the
/// current transform and Y increasing downwards.
/// </summary>
/// <remarks>
/// This is the single seam between the layout engine and any rendering backend. Every element draws through
/// this interface and never references a concrete graphics library, which is what allows the same document tree
/// to be rendered to a PDF, captured for tests, or measured without producing output.
/// </remarks>
public interface ISurface
{
    /// <summary>Pushes the current transform and clip onto a stack.</summary>
    void Save();

    /// <summary>Restores the transform and clip most recently pushed by <see cref="Save"/>.</summary>
    void Restore();

    void Translate(Offset offset);

    void Scale(float scaleX, float scaleY);

    /// <summary>Rotates clockwise about the current origin.</summary>
    void Rotate(float degrees);

    /// <summary>Restricts subsequent drawing to a rectangle at the current origin.</summary>
    void ClipRectangle(Extent size);

    void DrawRectangle(Offset position, Extent size, Ink color);

    /// <summary>
    /// Draws a rectangle with rounded corners.
    /// </summary>
    /// <param name="position">Top-left corner of the shape, relative to the current origin.</param>
    /// <param name="size">Outer extent of the shape.</param>
    /// <param name="cornerRadius">Corner rounding. Backends clamp anything larger than half the shorter side.</param>
    /// <param name="color">Fill or stroke colour, depending on <paramref name="strokeWidth"/>.</param>
    /// <param name="strokeWidth">Zero or less fills the shape; a positive value strokes an outline of that width.</param>
    void DrawRoundedRectangle(Offset position, Extent size, float cornerRadius, Ink color, float strokeWidth = 0f);

    void DrawLine(Offset from, Offset to, float thickness, Ink color);

    /// <summary>Draws a single run of text with its left edge on the baseline at <paramref name="baselineStart"/>.</summary>
    void DrawText(string text, Offset baselineStart, TypeStyle style);

    void DrawImage(IImage image, Extent size);

    /// <summary>Marks a rectangle at the current origin as a clickable link to an external URL.</summary>
    void DrawExternalLink(string url, Extent size);

    /// <summary>Marks a rectangle at the current origin as a link to a named destination within the document.</summary>
    void DrawInternalLink(string destinationName, Extent size);

    /// <summary>Registers a named destination at the current origin so internal links can target it.</summary>
    void DrawDestination(string destinationName);
}
