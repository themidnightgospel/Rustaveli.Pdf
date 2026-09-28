namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Paths: their segments, the cubics quadratics and arcs become, the shapes they add, and the box they reach.
/// </summary>
public class VectorPathTests
{
    /// <summary>A point on a cubic Bézier curve at <paramref name="t"/>.</summary>
    private static Offset On(Offset start, Offset first, Offset second, Offset end, float t)
    {
        float u = 1 - t;
        return new Offset(
            (u * u * u * start.X) + (3 * u * u * t * first.X) + (3 * u * t * t * second.X) + (t * t * t * end.X),
            (u * u * u * start.Y) + (3 * u * u * t * first.Y) + (3 * u * t * t * second.Y) + (t * t * t * end.Y));
    }

    /// <summary>Every point the path's curves pass through, sampled finely.</summary>
    private static List<Offset> Trace(VectorPath path)
    {
        List<Offset> trace = [];
        Offset current = Offset.Zero;
        int point = 0;

        foreach (PathVerb verb in path.Verbs)
        {
            switch (verb)
            {
                case PathVerb.Move:
                case PathVerb.Line:
                    current = path.Points[point++];
                    trace.Add(current);
                    break;

                case PathVerb.Cubic:
                    for (int step = 1; step <= 20; step++)
                        trace.Add(On(current, path.Points[point], path.Points[point + 1], path.Points[point + 2], step / 20f));

                    current = path.Points[point + 2];
                    point += 3;
                    break;
            }
        }

        return trace;
    }

    [Fact]
    public void SegmentsAreKeptInOrder()
    {
        VectorPath path = new VectorPath().MoveTo(1, 2).LineTo(3, 4).CurveTo(5, 6, 7, 8, 9, 10).Close();

        Assert.Equal([PathVerb.Move, PathVerb.Line, PathVerb.Cubic, PathVerb.Close], path.Verbs);
        Assert.Equal(
            [new Offset(1, 2), new Offset(3, 4), new Offset(5, 6), new Offset(7, 8), new Offset(9, 10)],
            path.Points);
        Assert.False(path.IsEmpty);
        Assert.True(new VectorPath().IsEmpty);
    }

    [Fact]
    public void ASegmentWithNoFigureStartsOneWhereThePenIs()
    {
        VectorPath path = new VectorPath().LineTo(10, 0);

        Assert.Equal([PathVerb.Move, PathVerb.Line], path.Verbs);
        Assert.Equal(Offset.Zero, path.Points[0]);
    }

    [Fact]
    public void AfterClosingTheNextSegmentStartsWhereTheFigureDid()
    {
        VectorPath path = new VectorPath().MoveTo(5, 5).LineTo(10, 5).Close().Close().LineTo(20, 20);

        Assert.Equal([PathVerb.Move, PathVerb.Line, PathVerb.Close, PathVerb.Move, PathVerb.Line], path.Verbs);
        Assert.Equal(new Offset(5, 5), path.Points[2]);
    }

    [Fact]
    public void AQuadraticIsRaisedToTheCubicThatDrawsIt()
    {
        VectorPath path = new VectorPath().MoveTo(0, 0).QuadraticTo(30, 60, 60, 0);

        Approximately.Equal(new Offset(20, 40), path.Points[1]);
        Approximately.Equal(new Offset(40, 40), path.Points[2]);
        Assert.Equal(new Offset(60, 0), path.Points[3]);

        // The quadratic's midpoint, a quarter of the way to its control point from the chord.
        Approximately.Equal(new Offset(30, 30), On(path.Points[0], path.Points[1], path.Points[2], path.Points[3], 0.5f));
    }

    [Theory]
    [InlineData(true, -50f)]
    [InlineData(false, 50f)]
    public void AHalfCircleArcBulgesToTheSideItsSweepSays(bool clockwise, float bulge)
    {
        // Clockwise on the page, Y running down: from left to right over the top.
        VectorPath path = new VectorPath().MoveTo(0, 0).ArcTo(50, 50, 0, largeArc: false, clockwise, 100, 0);
        List<Offset> trace = Trace(path);

        Assert.Equal(new Offset(100, 0), trace[trace.Count - 1]);
        Assert.All(trace, point => Assert.InRange(Math.Sqrt(Math.Pow(point.X - 50, 2) + Math.Pow(point.Y, 2)), 49.9, 50.1));
        Assert.Contains(trace, point => Math.Abs(point.X - 50) < 1 && Math.Abs(point.Y - bulge) < 0.2);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void EachOfTheFourArcsStaysOnItsEllipse(bool largeArc, bool clockwise)
    {
        // Two points 60 apart on circles of radius 50: the large arcs go the long way round.
        VectorPath path = new VectorPath().MoveTo(0, 0).ArcTo(50, 50, 0, largeArc, clockwise, 60, 0);
        List<Offset> trace = Trace(path);
        float centreY = largeArc == clockwise ? -40 : 40;

        Assert.All(trace, point => Assert.InRange(Math.Sqrt(Math.Pow(point.X - 30, 2) + Math.Pow(point.Y - centreY, 2)), 49.9, 50.1));
        // The small arcs turn 74 degrees, one cubic; the large ones 286, a cubic to each quarter turn begun.
        Assert.Equal(largeArc ? 4 : 1, path.Verbs.Count(verb => verb == PathVerb.Cubic));
    }

    [Fact]
    public void ATurnedEllipseKeepsItsShape()
    {
        // An ellipse turned a quarter: its long axis runs down, so the arc from its top to its bottom bulges 20 across.
        VectorPath path = new VectorPath().MoveTo(0, -40).ArcTo(40, 20, 90, largeArc: false, clockwise: true, 0, 40);
        List<Offset> trace = Trace(path);

        Assert.Contains(trace, point => Math.Abs(point.Y) < 1 && Math.Abs(point.X - 20) < 0.2);
    }

    [Fact]
    public void RadiiTooSmallToReachAreScaledUp()
    {
        VectorPath path = new VectorPath().MoveTo(0, 0).ArcTo(10, 10, 0, largeArc: false, clockwise: true, 100, 0);
        List<Offset> trace = Trace(path);

        Assert.All(trace, point => Assert.InRange(Math.Sqrt(Math.Pow(point.X - 50, 2) + Math.Pow(point.Y, 2)), 49.9, 50.1));
    }

    [Fact]
    public void AnArcWithNoRadiusIsAStraightLine()
    {
        VectorPath path = new VectorPath().MoveTo(0, 0).ArcTo(0, 10, 0, false, true, 30, 40);

        Assert.Equal([PathVerb.Move, PathVerb.Line], path.Verbs);
        Assert.Equal(new Offset(30, 40), path.Points[1]);
        Assert.Equal([PathVerb.Move, PathVerb.Line], new VectorPath().MoveTo(0, 0).ArcTo(10, 0, 0, false, true, 30, 40).Verbs);
    }

    [Fact]
    public void AnArcEndingWhereItStartsIsNothing()
    {
        VectorPath path = new VectorPath().MoveTo(5, 5).ArcTo(10, 10, 0, false, true, 5, 5);

        Assert.Equal([PathVerb.Move], path.Verbs);
    }

    [Fact]
    public void ARectangleIsAClosedFigureFromItsTopLeft()
    {
        VectorPath path = new VectorPath().AddRectangle(10, 20, 30, 40);

        Assert.Equal([PathVerb.Move, PathVerb.Line, PathVerb.Line, PathVerb.Line, PathVerb.Close], path.Verbs);
        Assert.Equal([new Offset(10, 20), new Offset(40, 20), new Offset(40, 60), new Offset(10, 60)], path.Points);
    }

    [Fact]
    public void ACircleStaysOnItsRadius()
    {
        VectorPath path = new VectorPath().AddCircle(50, 50, 20);

        Assert.Equal(4, path.Verbs.Count(verb => verb == PathVerb.Cubic));
        Assert.All(Trace(path), point => Assert.InRange(Math.Sqrt(Math.Pow(point.X - 50, 2) + Math.Pow(point.Y - 50, 2)), 19.95, 20.05));
    }

    [Fact]
    public void AnEllipseReachesItsRadiiEachWay()
    {
        (Offset position, Extent size) = new VectorPath().AddEllipse(50, 40, 30, 10).Bounds();

        Assert.Equal(new Offset(20, 30), position);
        Assert.Equal(new Extent(60, 20), size);
    }

    [Fact]
    public void ARoundedRectangleCurvesOnlyItsRoundedCorners()
    {
        VectorPath path = new VectorPath().AddRoundedRectangle(0, 0, 100, 50, new Corners(10, 0, 20, 0));
        List<Offset> trace = Trace(path);

        Assert.Equal(2, path.Verbs.Count(verb => verb == PathVerb.Cubic));
        Assert.Contains(new Offset(100, 0), trace);
        Assert.Contains(new Offset(0, 50), trace);
        Assert.DoesNotContain(new Offset(0, 0), trace);
        Assert.DoesNotContain(new Offset(100, 50), trace);

        // The top left arc stays on its circle.
        Assert.All(
            trace.Where(point => point.X < 10 && point.Y < 10),
            point => Assert.InRange(Math.Sqrt(Math.Pow(point.X - 10, 2) + Math.Pow(point.Y - 10, 2)), 9.95, 10.05));
    }

    [Fact]
    public void ARoundedRectangleWithSquareCornersIsARectangle() =>
        Assert.Equal(new VectorPath().AddRectangle(0, 0, 10, 10).Verbs, new VectorPath().AddRoundedRectangle(0, 0, 10, 10, Corners.Zero).Verbs);

    [Fact]
    public void EveryCornerOfARoundedRectangleCanBeRounded()
    {
        VectorPath path = new VectorPath().AddRoundedRectangle(0, 0, 100, 50, Corners.All(10));

        Assert.Equal(4, path.Verbs.Count(verb => verb == PathVerb.Cubic));
        Assert.All(
            Trace(path).Where(point => point.X > 90 && point.Y > 40),
            point => Assert.InRange(Math.Sqrt(Math.Pow(point.X - 90, 2) + Math.Pow(point.Y - 40, 2)), 9.95, 10.05));
    }

    [Fact]
    public void TheBoundsTakeInEveryPoint()
    {
        (Offset position, Extent size) = new VectorPath().MoveTo(10, 10).CurveTo(-5, 40, 50, 60, 30, 20).Bounds();

        Assert.Equal(new Offset(-5, 10), position);
        Assert.Equal(new Extent(55, 50), size);
        Assert.Equal((Offset.Zero, Extent.Zero), new VectorPath().Bounds());
    }
}
