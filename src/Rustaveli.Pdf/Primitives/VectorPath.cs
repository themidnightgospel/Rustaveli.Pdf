namespace Rustaveli.Pdf;

/// <summary>
/// A path of straight and curved segments, in one or more closed or open figures, for artwork to fill, stroke or clip
/// to. Coordinates are in points, Y running down.
/// </summary>
/// <remarks>
/// Quadratic curves and elliptical arcs are kept as the cubic curves that draw them exactly (quadratics) or to within a
/// hair's breadth (arcs, a quarter turn to a cubic), so every surface draws the same segments.
/// </remarks>
public sealed class VectorPath
{
    /// <summary>Control-point distance, as a fraction of the radius, of a cubic Bézier approximating a quarter circle.</summary>
    private const double Kappa = 0.5522847498307936;

    private readonly List<PathVerb> _verbs = [];
    private readonly List<Offset> _points = [];
    private Offset _current;
    private Offset _figureStart;
    private bool _open;

    /// <summary>Whether the path has no segments.</summary>
    public bool IsEmpty => _verbs.Count == 0;

    internal IReadOnlyList<PathVerb> Verbs => _verbs;

    /// <summary>The points each verb takes, in order: one for a move or line, three for a curve, none for a close.</summary>
    internal IReadOnlyList<Offset> Points => _points;

    /// <summary>Starts a new figure at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public VectorPath MoveTo(float x, float y)
    {
        _verbs.Add(PathVerb.Move);
        _points.Add(new Offset(x, y));
        _current = _figureStart = new Offset(x, y);
        _open = true;
        return this;
    }

    /// <summary>A straight segment to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public VectorPath LineTo(float x, float y)
    {
        StartIfNeeded();
        _verbs.Add(PathVerb.Line);
        _points.Add(new Offset(x, y));
        _current = new Offset(x, y);
        return this;
    }

    /// <summary>A cubic Bézier curve to (<paramref name="x"/>, <paramref name="y"/>) through two control points.</summary>
    public VectorPath CurveTo(float x1, float y1, float x2, float y2, float x, float y)
    {
        StartIfNeeded();
        _verbs.Add(PathVerb.Cubic);
        _points.Add(new Offset(x1, y1));
        _points.Add(new Offset(x2, y2));
        _points.Add(new Offset(x, y));
        _current = new Offset(x, y);
        return this;
    }

    /// <summary>A quadratic Bézier curve to (<paramref name="x"/>, <paramref name="y"/>) through one control point.</summary>
    public VectorPath QuadraticTo(float controlX, float controlY, float x, float y)
    {
        StartIfNeeded();
        Offset start = _current;

        // Raised to a cubic, which draws the same curve: each control point two thirds of the way to the one given.
        return CurveTo(
            start.X + (2f / 3 * (controlX - start.X)),
            start.Y + (2f / 3 * (controlY - start.Y)),
            x + (2f / 3 * (controlX - x)),
            y + (2f / 3 * (controlY - y)),
            x,
            y);
    }

    /// <summary>
    /// An elliptical arc to (<paramref name="x"/>, <paramref name="y"/>), as SVG draws one: radii, the ellipse's turn in
    /// degrees, and which of the four possible arcs — the large or the small, clockwise or not.
    /// </summary>
    public VectorPath ArcTo(float radiusX, float radiusY, float rotation, bool largeArc, bool clockwise, float x, float y)
    {
        StartIfNeeded();
        Offset start = _current;
        double rx = Math.Abs(radiusX);
        double ry = Math.Abs(radiusY);

        // An arc with no radius, or ending where it starts, is a straight line or nothing, as SVG says.
        if (rx == 0 || ry == 0)
            return LineTo(x, y);

        if (start.X == x && start.Y == y)
            return this;

        double phi = rotation * Math.PI / 180;
        double cos = Math.Cos(phi);
        double sin = Math.Sin(phi);

        // The endpoints moved to the ellipse's own axes, from the midpoint between them (SVG implementation notes, F.6.5).
        double dx = (start.X - x) / 2.0;
        double dy = (start.Y - y) / 2.0;
        double x1 = (cos * dx) + (sin * dy);
        double y1 = (-sin * dx) + (cos * dy);

        // Radii too small to reach are scaled up until they just do.
        double reach = ((x1 * x1) / (rx * rx)) + ((y1 * y1) / (ry * ry));
        if (reach > 1)
        {
            rx *= Math.Sqrt(reach);
            ry *= Math.Sqrt(reach);
        }

        double numerator = (rx * rx * ry * ry) - (rx * rx * y1 * y1) - (ry * ry * x1 * x1);
        double denominator = (rx * rx * y1 * y1) + (ry * ry * x1 * x1);
        double factor = Math.Sqrt(Math.Max(0, numerator / denominator)) * (largeArc == clockwise ? -1 : 1);
        double cx1 = factor * rx * y1 / ry;
        double cy1 = -factor * ry * x1 / rx;

        double centreX = (cos * cx1) - (sin * cy1) + ((start.X + x) / 2.0);
        double centreY = (sin * cx1) + (cos * cy1) + ((start.Y + y) / 2.0);

        double startAngle = Angle(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry);
        double sweep = Angle((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry);

        if (!clockwise && sweep > 0)
            sweep -= 2 * Math.PI;
        else if (clockwise && sweep < 0)
            sweep += 2 * Math.PI;

        // No more than a quarter turn to each cubic, which keeps the error under a thousandth of the radius.
        int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) - 1e-9));
        double step = sweep / pieces;
        double handle = 4.0 / 3 * Math.Tan(step / 4);
        double angle = startAngle;

        for (int piece = 0; piece < pieces; piece++)
        {
            double next = angle + step;
            (double ax, double ay) = OnEllipse(angle);
            (double bx, double by) = OnEllipse(next);
            (double adx, double ady) = Tangent(angle);
            (double bdx, double bdy) = Tangent(next);

            bool last = piece == pieces - 1;
            CurveTo(
                (float)(ax + (handle * adx)),
                (float)(ay + (handle * ady)),
                (float)(bx - (handle * bdx)),
                (float)(by - (handle * bdy)),
                last ? x : (float)bx,
                last ? y : (float)by);

            angle = next;
        }

        return this;

        (double X, double Y) OnEllipse(double theta) =>
            (centreX + (rx * Math.Cos(theta) * cos) - (ry * Math.Sin(theta) * sin),
             centreY + (rx * Math.Cos(theta) * sin) + (ry * Math.Sin(theta) * cos));

        (double X, double Y) Tangent(double theta) =>
            ((-rx * Math.Sin(theta) * cos) - (ry * Math.Cos(theta) * sin),
             (-rx * Math.Sin(theta) * sin) + (ry * Math.Cos(theta) * cos));
    }

    /// <summary>Closes the figure with a straight segment back to where it started.</summary>
    public VectorPath Close()
    {
        if (!_open)
            return this;

        _verbs.Add(PathVerb.Close);
        _current = _figureStart;
        _open = false;
        return this;
    }

    /// <summary>A rectangle, as a closed figure drawn clockwise from its top left.</summary>
    public VectorPath AddRectangle(float x, float y, float width, float height) =>
        MoveTo(x, y).LineTo(x + width, y).LineTo(x + width, y + height).LineTo(x, y + height).Close();

    /// <summary>A rectangle with each corner rounded to its own radius, fitted to the rectangle as CSS fits them.</summary>
    public VectorPath AddRoundedRectangle(float x, float y, float width, float height, Corners corners)
    {
        Corners radii = corners.FittedTo(new Extent(width, height));

        if (!radii.IsRounded)
            return AddRectangle(x, y, width, height);

        float right = x + width;
        float bottom = y + height;
        float k = (float)Kappa;

        MoveTo(x + radii.TopLeft, y).LineTo(right - radii.TopRight, y);
        Corner(right - radii.TopRight, y, right, y + radii.TopRight, radii.TopRight, 1, 0, 0, 1);
        LineTo(right, bottom - radii.BottomRight);
        Corner(right, bottom - radii.BottomRight, right - radii.BottomRight, bottom, radii.BottomRight, 0, 1, -1, 0);
        LineTo(x + radii.BottomLeft, bottom);
        Corner(x + radii.BottomLeft, bottom, x, bottom - radii.BottomLeft, radii.BottomLeft, -1, 0, 0, -1);
        LineTo(x, y + radii.TopLeft);
        Corner(x, y + radii.TopLeft, x + radii.TopLeft, y, radii.TopLeft, 0, -1, 1, 0);
        return Close();

        // A quarter circle from one point to the next, leaving in one direction and arriving in another.
        void Corner(float fromX, float fromY, float toX, float toY, float radius, float outX, float outY, float inX, float inY)
        {
            if (radius > 0)
                CurveTo(fromX + (outX * radius * k), fromY + (outY * radius * k), toX - (inX * radius * k), toY - (inY * radius * k), toX, toY);
        }
    }

    /// <summary>An ellipse centred at (<paramref name="centreX"/>, <paramref name="centreY"/>), as a closed figure.</summary>
    public VectorPath AddEllipse(float centreX, float centreY, float radiusX, float radiusY)
    {
        float kx = (float)(radiusX * Kappa);
        float ky = (float)(radiusY * Kappa);
        float left = centreX - radiusX;
        float right = centreX + radiusX;
        float top = centreY - radiusY;
        float bottom = centreY + radiusY;

        return MoveTo(right, centreY)
            .CurveTo(right, centreY + ky, centreX + kx, bottom, centreX, bottom)
            .CurveTo(centreX - kx, bottom, left, centreY + ky, left, centreY)
            .CurveTo(left, centreY - ky, centreX - kx, top, centreX, top)
            .CurveTo(centreX + kx, top, right, centreY - ky, right, centreY)
            .Close();
    }

    /// <summary>A circle centred at (<paramref name="centreX"/>, <paramref name="centreY"/>), as a closed figure.</summary>
    public VectorPath AddCircle(float centreX, float centreY, float radius) => AddEllipse(centreX, centreY, radius, radius);

    /// <summary>
    /// Adds every figure of <paramref name="path"/>, each point mapped (x, y) to (a·x + c·y + e, b·x + d·y + f): a
    /// cubic mapped so is the cubic of the mapped points.
    /// </summary>
    internal VectorPath AddTransformed(VectorPath path, float a, float b, float c, float d, float e, float f)
    {
        int point = 0;

        foreach (PathVerb verb in path._verbs)
        {
            switch (verb)
            {
                case PathVerb.Move:
                    MoveTo(X(point), Y(point));
                    point++;
                    break;

                case PathVerb.Line:
                    LineTo(X(point), Y(point));
                    point++;
                    break;

                case PathVerb.Cubic:
                    CurveTo(X(point), Y(point), X(point + 1), Y(point + 1), X(point + 2), Y(point + 2));
                    point += 3;
                    break;

                default:
                    Close();
                    break;
            }
        }

        return this;

        float X(int index) => (a * path._points[index].X) + (c * path._points[index].Y) + e;

        float Y(int index) => (b * path._points[index].X) + (d * path._points[index].Y) + f;
    }

    /// <summary>The box every point of the path, control points included, lies within; empty for an empty path.</summary>
    internal (Offset Position, Extent Size) Bounds()
    {
        if (_points.Count == 0)
            return (Offset.Zero, Extent.Zero);

        float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;

        foreach (Offset point in _points)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        return (new Offset(left, top), new Extent(right - left, bottom - top));
    }

    /// <summary>A segment drawn without a figure started begins one where the pen is.</summary>
    private void StartIfNeeded()
    {
        if (!_open)
            MoveTo(_current.X, _current.Y);
    }

    /// <summary>The signed angle from one vector to another, in radians.</summary>
    private static double Angle(double ux, double uy, double vx, double vy)
    {
        double angle = Math.Atan2((ux * vy) - (uy * vx), (ux * vx) + (uy * vy));
        return angle;
    }
}
