using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// What blocks draw on. Layout never reaches a graphics library itself, so one document can be written as PDF, drawn
/// as page images, recorded by tests, held back and replayed in draw order, or counted without being drawn at all.
/// </summary>
/// <remarks>
/// <para>
/// Everything is in points, from the top left of the current space, Y running down. The surface keeps a current
/// transform and a current clip, which only the transform and clip members change: <see cref="Save"/> keeps both and
/// <see cref="Restore"/> returns to them, and callers balance the two within a page. Positions given to the drawing
/// members are relative to the current origin and pass through the whole transform in force.
/// </para>
/// <para>
/// Every implementation gives each member the same meaning, so the same content lands in the same place whichever
/// surface draws it. Nothing a surface does reports back to layout except <see cref="Origin"/>, so drawing can never
/// change what was laid out.
/// </para>
/// </remarks>
internal interface ISurface
{
    /// <summary>
    /// Where the current origin lies on the page, in points from its top left with Y running down, after every
    /// transform in force. Blocks read it to record where content was placed, so it must be right even on surfaces
    /// that draw nothing.
    /// </summary>
    Offset Origin { get; }

    void Save();

    void Restore();

    /// <summary>Moves the origin by <paramref name="distance"/>, measured in the current space.</summary>
    void MoveOrigin(Offset distance);

    /// <summary>
    /// Scales the current space about its origin, each axis by its own factor. A negative factor mirrors that axis.
    /// </summary>
    void ScaleAxes(float horizontal, float vertical);

    /// <summary>Turns the current space clockwise on the page about its origin, by <paramref name="degrees"/>.</summary>
    void RotateClockwise(float degrees);

    /// <summary>Applies the affine matrix [a b c d e f] after the transform in force, in PDF's order of terms.</summary>
    void Concatenate(float a, float b, float c, float d, float e, float f);

    /// <summary>Limits drawing to a rectangle of <paramref name="size"/> at the origin, until the next restore.</summary>
    void ClipRectangle(Extent size);

    /// <summary>
    /// Fills a rectangle of <paramref name="size"/> whose top left is at <paramref name="topLeft"/>. A transparent ink,
    /// or a size with no area, draws nothing; while a gradient is set, the ink only decides whether the shape is
    /// drawn, and the gradient paints it.
    /// </summary>
    void FillRectangle(Offset topLeft, Extent size, Ink ink);

    /// <summary>
    /// Fills a rectangle with rounded corners, or strokes its outline when <paramref name="strokeWidth"/> is more than
    /// zero.
    /// </summary>
    void DrawRoundedRectangle(Offset position, Extent size, Corners corners, Ink color, float strokeWidth = 0f);

    void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid);

    void DrawDashedLine(Offset from, Offset to, float thickness, Ink color, IReadOnlyList<float> pattern);

    /// <summary>
    /// Paints the shapes drawn from here to <see cref="EndGradient"/> with <paramref name="gradient"/>, laid across a
    /// box of <paramref name="size"/> at <paramref name="position"/>, instead of in their own ink.
    /// </summary>
    void BeginGradient(Gradient gradient, Offset position, Extent size);

    void EndGradient();

    /// <summary>Draws the shadow a box of <paramref name="size"/> at <paramref name="position"/> casts.</summary>
    void DrawShadow(Offset position, Extent size, Corners corners, Shadow shadow);

    void FillPath(VectorPath path, Ink ink, FillRule rule);

    void StrokePath(VectorPath path, Ink ink, LineStyle style);

    /// <summary>Limits drawing to the inside of <paramref name="path"/>, until the next restore.</summary>
    void ClipPath(VectorPath path, FillRule rule);

    /// <summary>
    /// Sets a run of text in <paramref name="style"/>, beginning on the baseline at <paramref name="baseline"/>.
    /// </summary>
    /// <remarks>
    /// The text is given in logical order. A right-to-left run is shaped in that order and then set with its first
    /// character at the right, brackets and other mirrorable characters drawn mirrored. Empty text, a transparent
    /// ink or a size of nothing draws nothing, and no text is drawn while a gradient is set.
    /// </remarks>
    void ShowText(string text, Offset baseline, TypeStyle style, ReadingDirection direction);

    /// <summary>
    /// Paints <paramref name="image"/> to fill a box of <paramref name="size"/> whose top left is the current origin. A
    /// size with no area draws nothing.
    /// </summary>
    void PaintImage(IImage image, Extent size);

    /// <summary>
    /// Makes a box of <paramref name="size"/> at <paramref name="topLeft"/> a link that opens <paramref name="url"/>.
    /// An empty address makes nothing. In a tagged document the link belongs to the Link element entered last.
    /// </summary>
    /// <remarks>
    /// The box is placed where the content it covers was drawn, through the whole transform in force; under a turn or
    /// a scale, a PDF link takes the smallest upright box on the page that holds it.
    /// </remarks>
    void LinkToUrl(string url, Offset topLeft, Extent size);

    /// <summary>
    /// Makes a box of <paramref name="size"/> at <paramref name="topLeft"/> a link to the place in the same document
    /// named <paramref name="destination"/>. An empty name makes nothing; tagging and placement are as for
    /// <see cref="LinkToUrl"/>.
    /// </summary>
    void LinkToDestination(string destination, Offset topLeft, Extent size);

    /// <summary>
    /// Names the point <paramref name="at"/> on the current page, so links and cross-references can lead to it. The
    /// first point given a name keeps it; an empty name names nothing.
    /// </summary>
    void NameDestination(string name, Offset at);

    /// <summary>Adds an entry to the document's outline, at <paramref name="level"/>, leading to the current origin.</summary>
    void DrawBookmark(string title, int level);

    /// <summary>
    /// Marks what is drawn from here on as content of <paramref name="element"/> in the document's structure, or as
    /// an artifact outside it when null.
    /// </summary>
    void Tag(StructureElement? element);
}
