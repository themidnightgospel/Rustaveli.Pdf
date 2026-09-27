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
internal interface ISurface
{
    /// <summary>Pushes the current transform and clip onto a stack.</summary>
    /// <summary>
    /// Where the current origin lies on the page, in points from the page's top left with Y running down, after
    /// every translation, scale and turn in force.
    /// </summary>
    Offset Origin { get; }

    void Save();

    /// <summary>Restores the transform and clip most recently pushed by <see cref="Save"/>.</summary>
    void Restore();

    void Translate(Offset offset);

    void Scale(float scaleX, float scaleY);

    /// <summary>Rotates clockwise about the current origin.</summary>
    void Rotate(float degrees);

    /// <summary>
    /// Applies a general transform, mapping (x, y) to (a·x + c·y + e, b·x + d·y + f) in the space in force, as SVG's
    /// <c>matrix(a b c d e f)</c> does.
    /// </summary>
    void Concatenate(float a, float b, float c, float d, float e, float f);

    /// <summary>Restricts subsequent drawing to a rectangle at the current origin.</summary>
    void ClipRectangle(Extent size);

    void DrawRectangle(Offset position, Extent size, Ink color);

    /// <summary>
    /// Draws a rectangle with rounded corners.
    /// </summary>
    /// <param name="position">Top-left corner of the shape, relative to the current origin.</param>
    /// <param name="size">Outer extent of the shape.</param>
    /// <param name="corners">Each corner's rounding, fitted to the shape as CSS fits it: radii that would overlap are scaled down together.</param>
    /// <param name="color">Fill or stroke colour, depending on <paramref name="strokeWidth"/>.</param>
    /// <param name="strokeWidth">Zero or less fills the shape; a positive value strokes an outline of that width.</param>
    void DrawRoundedRectangle(Offset position, Extent size, Corners corners, Ink color, float strokeWidth = 0f);

    /// <summary>
    /// Strokes a line from <paramref name="from"/> to <paramref name="to"/>. A double line is two strokes a third of
    /// <paramref name="thickness"/> each, with a gap between them; dots and dashes are sized from the thickness; a wave
    /// swings a thickness either side of the line.
    /// </summary>
    void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid);

    /// <summary>
    /// Strokes a line in dashes and gaps of the lengths in <paramref name="pattern"/>, alternating and starting with a
    /// dash, repeated along its length. The lengths are never negative and not all zero.
    /// </summary>
    void DrawDashedLine(Offset from, Offset to, float thickness, Ink color, IReadOnlyList<float> pattern);

    /// <summary>
    /// Paints the rectangles, lines and outlines that follow in <paramref name="gradient"/> instead of their own ink,
    /// laid across the box at <paramref name="position"/> of <paramref name="size"/>, until <see cref="EndGradient"/>.
    /// Their ink still decides whether they are drawn at all. Text is not drawn meanwhile.
    /// </summary>
    void BeginGradient(Gradient gradient, Offset position, Extent size);

    /// <summary>Returns to painting in each shape's own ink.</summary>
    void EndGradient();

    /// <summary>
    /// Draws the shadow a rectangle at <paramref name="position"/> of <paramref name="size"/>, rounded to
    /// <paramref name="corners"/>, casts: moved, grown and blurred as <paramref name="shadow"/> says.
    /// </summary>
    void DrawShadow(Offset position, Extent size, Corners corners, Shadow shadow);

    /// <summary>Fills <paramref name="path"/>, deciding what is inside by <paramref name="rule"/>.</summary>
    void FillPath(VectorPath path, Ink ink, FillRule rule);

    /// <summary>Strokes <paramref name="path"/> as <paramref name="style"/> says.</summary>
    void StrokePath(VectorPath path, Ink ink, LineStyle style);

    /// <summary>Confines what is drawn after it, until the state is restored, to the inside of <paramref name="path"/>.</summary>
    void ClipPath(VectorPath path, FillRule rule);

    /// <summary>Draws a single run of text with its left edge on the baseline at <paramref name="baselineStart"/>.</summary>
    /// <param name="text">The text, in logical order.</param>
    /// <param name="baselineStart">Where the run begins, on the baseline.</param>
    /// <param name="style">The type it is set in.</param>
    /// <param name="rightToLeft">
    /// Whether the run reads right to left: shaped in logical order, then set with its first character at the right,
    /// and characters with a mirror image, such as brackets, drawn as that image.
    /// </param>
    void DrawText(string text, Offset baselineStart, TypeStyle style, bool rightToLeft = false);

    void DrawImage(IImage image, Extent size);

    /// <summary>Marks a rectangle at the current origin as a clickable link to an external URL.</summary>
    void DrawExternalLink(string url, Extent size);

    /// <summary>Marks a rectangle at the current origin as a link to a named destination within the document.</summary>
    void DrawInternalLink(string destinationName, Extent size);

    /// <summary>Registers a named destination at the current origin so internal links can target it.</summary>
    void DrawDestination(string destinationName);

    /// <summary>
    /// Adds a bookmark to the document's outline, titled <paramref name="title"/> at <paramref name="level"/> from 1
    /// for the outermost, leading to the current origin.
    /// </summary>
    void DrawBookmark(string title, int level);
}
