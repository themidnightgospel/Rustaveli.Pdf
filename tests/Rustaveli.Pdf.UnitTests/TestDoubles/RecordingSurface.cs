using System.Numerics;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

internal sealed class RecordingSurface : IPageSink, ISurface, IDisposable
{
    private readonly Stack<Matrix3x2> _saved = new Stack<Matrix3x2>();

    private Matrix3x2 _transform = Matrix3x2.Identity;

    private RecordedPage? _current;

    private const float Tolerance = 0.001f;

    public List<RecordedPage> Pages { get; } = new List<RecordedPage>();

    private RecordedPage Current => _current ?? throw new InvalidOperationException("No page is open.");

    public bool IsAtIdentity => Math.Abs(_transform.M11 - 1f) < 0.001f && Math.Abs(_transform.M22 - 1f) < 0.001f && Math.Abs(_transform.M12) < 0.001f && Math.Abs(_transform.M21) < 0.001f && Math.Abs(_transform.M31) < 0.001f && Math.Abs(_transform.M32) < 0.001f;

    public int PendingSaves => _saved.Count;

    public RecordedPage Page(int oneBasedNumber)
    {
        return Pages[oneBasedNumber - 1];
    }

    public void BeginPage(Extent size)
    {
        _current = new RecordedPage(size);
        _transform = Matrix3x2.Identity;
        _saved.Clear();
        Pages.Add(_current);
    }

    public void EndPage()
    {
        _current = null;
    }

    public Offset Origin => Resolve(Offset.Zero);

    public void Save()
    {
        _saved.Push(_transform);
    }

    public void Restore()
    {
        _transform = _saved.Pop();
    }

    public void MoveOrigin(Offset distance)
    {
        _transform = Matrix3x2.CreateTranslation(distance.X, distance.Y) * _transform;
    }

    public void ScaleAxes(float horizontal, float vertical)
    {
        _transform = Matrix3x2.CreateScale(horizontal, vertical) * _transform;
    }

    public void Concatenate(float a, float b, float c, float d, float e, float f)
    {
        _transform = new Matrix3x2(a, b, c, d, e, f) * _transform;
    }

    public void FillPath(VectorPath path, Ink ink, FillRule rule)
    {
        Current.Operations.Add(new PathOperation(Resolve(Offset.Zero), PathPainting.Fill, path, ink, rule, null, PathBounds(path)));
    }

    public void StrokePath(VectorPath path, Ink ink, LineStyle style)
    {
        Current.Operations.Add(new PathOperation(Resolve(Offset.Zero), PathPainting.Stroke, path, ink, FillRule.NonZero, style, PathBounds(path)));
    }

    public void ClipPath(VectorPath path, FillRule rule)
    {
        Current.Operations.Add(new PathOperation(Resolve(Offset.Zero), PathPainting.Clip, path, Ink.Transparent, rule, null, PathBounds(path)));
    }

    /// <summary>The box the path's points reach, on the page.</summary>
    private Bounds PathBounds(VectorPath path)
    {
        if (path.IsEmpty)
            return new Bounds(0, 0, 0, 0);

        List<Offset> points = path.Points.Select(Resolve).ToList();
        return new Bounds(points.Min(point => point.X), points.Min(point => point.Y), points.Max(point => point.X), points.Max(point => point.Y));
    }

    public void RotateClockwise(float degrees)
    {
        _transform = Matrix3x2.CreateRotation(degrees * (float)Math.PI / 180f) * _transform;
    }

    public void ClipRectangle(Extent size)
    {
    }

    public void FillRectangle(Offset topLeft, Extent size, Ink ink)
    {
        Current.Operations.Add(new RectangleOperation(Resolve(topLeft), size, ink, ResolveBounds(topLeft, size)));
    }

    public void DrawRoundedRectangle(Offset position, Extent size, Corners corners, Ink color, float strokeWidth = 0f)
    {
        Current.Operations.Add(new RoundedRectangleOperation(Resolve(position), size, corners, color, strokeWidth, ResolveBounds(position, size)));
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid)
    {
        Current.Operations.Add(new LineOperation(Resolve(from), Resolve(to), thickness, color, style));
    }

    public void DrawDashedLine(Offset from, Offset to, float thickness, Ink color, IReadOnlyList<float> pattern)
    {
        Current.Operations.Add(new LineOperation(Resolve(from), Resolve(to), thickness, color, StrokeStyle.Solid, pattern));
    }

    public void BeginGradient(Gradient gradient, Offset position, Extent size)
    {
        Current.Operations.Add(new GradientOperation(Resolve(position), size, gradient, ResolveBounds(position, size)));
    }

    public void EndGradient()
    {
        Current.Operations.Add(new GradientEndOperation());
    }

    public void DrawShadow(Offset position, Extent size, Corners corners, Shadow shadow)
    {
        Current.Operations.Add(new ShadowOperation(Resolve(position), size, corners, shadow, ResolveBounds(position, size)));
    }

    public void ShowText(ReadOnlyMemory<char> text, Offset baseline, TypeStyle style, ReadingDirection direction)
    {
        Current.Operations.Add(new TextOperation(Resolve(baseline), text.ToString(), style, direction == ReadingDirection.RightToLeft));
    }

    public void PaintImage(IImage image, Extent size)
    {
        Current.Operations.Add(new ImageOperation(Resolve(Offset.Zero), size, ResolveBounds(Offset.Zero, size)));
    }

    public void LinkToUrl(string url, Offset topLeft, Extent size)
    {
        Current.Operations.Add(new ExternalLinkOperation(Resolve(topLeft), size, url, ResolveBounds(topLeft, size)));
    }

    public void LinkToDestination(string destination, Offset topLeft, Extent size)
    {
        Current.Operations.Add(new InternalLinkOperation(Resolve(topLeft), size, destination, ResolveBounds(topLeft, size)));
    }

    public void NameDestination(string name, Offset at)
    {
        Current.Operations.Add(new DestinationOperation(Resolve(at), name));
    }

    public void DrawBookmark(string title, int level)
    {
        Current.Operations.Add(new BookmarkOperation(Resolve(Offset.Zero), title, level));
    }

    /// <summary>The element content is drawn for, as last named, and whether one has been named at all.</summary>
    public StructureElement? CurrentTag { get; private set; }

    /// <summary>The document the first element named descends from.</summary>
    public StructureElement? Root { get; private set; }

    public void Tag(StructureElement? element)
    {
        CurrentTag = element;
        _current?.Operations.Add(new TagOperation(Resolve(Offset.Zero), element));

        for (StructureElement? ancestor = element; Root is null && ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.Parent is null)
                Root = ancestor;
        }
    }

    private Offset Resolve(Offset position)
    {
        Vector2 vector = Vector2.Transform(new Vector2(position.X, position.Y), _transform);
        return new Offset(vector.X, vector.Y);
    }

    private Bounds ResolveBounds(Offset position, Extent size)
    {
        Offset position2 = Resolve(position);
        Offset position3 = Resolve(position + new Offset(size.Width, size.Height));
        return new Bounds(Math.Min(position2.X, position3.X), Math.Min(position2.Y, position3.Y), Math.Max(position2.X, position3.X), Math.Max(position2.Y, position3.Y));
    }

    public void Dispose()
    {
    }
}
