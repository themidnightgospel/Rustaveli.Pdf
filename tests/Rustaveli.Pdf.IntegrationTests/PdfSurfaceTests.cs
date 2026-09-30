using System.IO.Compression;
using System.Text;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.Tagging;
using Rustaveli.Pdf.Text;
using UglyToad.PdfPig;
using PdfDocumentWriter = Rustaveli.Pdf.Writing.PdfDocumentWriter;
using PdfWriterOptions = Rustaveli.Pdf.Writing.PdfWriterOptions;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Graphics.Core;
using UglyToad.PdfPig.Tokens;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Drives the PDF surface directly and reads the file back with an independent parser.
/// </summary>
/// <remarks>
/// Document-level tests reach the surface only through what the layout engine happens to emit, which never
/// includes an empty link, a zero-width rule or a clip. Those inputs are part of its contract all the same. PDF puts
/// the origin at the bottom-left with Y upwards, so every expected vertical position here is the page height minus
/// the surface coordinate.
/// </remarks>
public class PdfSurfaceTests
{
    private const float PageSide = 200f;
    private const double Tolerance = 0.5;

    private static readonly Ink Brick = Ink.Rgb(200, 40, 40);
    private static readonly Ink Ocean = Ink.Rgb(10, 120, 230);
    private static readonly TypeStyle Style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(20);

    /// <summary>A character outside the Basic Multilingual Plane: one character, two UTF-16 code units.</summary>
    private const string MathBoldA = "\U0001D400";

    private static readonly Dictionary<string, Action<PdfSurface>> Operations = new Dictionary<string, Action<PdfSurface>>
    {
        ["Save"] = surface => surface.Save(),
        ["Restore"] = surface => surface.Restore(),
        ["MoveOrigin"] = surface => surface.MoveOrigin(new Offset(5, 5)),
        ["ScaleAxes"] = surface => surface.ScaleAxes(2, 2),
        ["RotateClockwise"] = surface => surface.RotateClockwise(90),
        ["ClipRectangle"] = surface => surface.ClipRectangle(new Extent(10, 10)),
        ["FillRectangle"] = surface => surface.FillRectangle(Offset.Zero, new Extent(10, 10), Brick),
        ["DrawRoundedRectangle"] = surface => surface.DrawRoundedRectangle(Offset.Zero, new Extent(10, 10), Corners.All(2), Brick),
        ["DrawLine"] = surface => surface.DrawLine(Offset.Zero, new Offset(10, 0), 1, Brick),
        ["DrawDashedLine"] = surface => surface.DrawDashedLine(Offset.Zero, new Offset(10, 0), 1, Brick, [2, 1]),
        ["Concatenate"] = surface => surface.Concatenate(1, 0, 0, 1, 5, 5),
        ["FillPath"] = surface => surface.FillPath(new VectorPath().AddRectangle(0, 0, 5, 5), Brick, FillRule.NonZero),
        ["StrokePath"] = surface => surface.StrokePath(new VectorPath().AddRectangle(0, 0, 5, 5), Brick, new LineStyle(1)),
        ["ClipPath"] = surface => surface.ClipPath(new VectorPath().AddRectangle(0, 0, 5, 5), FillRule.NonZero),
        ["ShowText"] = surface => surface.ShowText("Text", new Offset(10, 30), Style, ReadingDirection.LeftToRight),
        ["PaintImage"] = surface =>
        {
            RasterImage image = RasterImage.FromBytes(TestImages.Png(4, 2));
            surface.PaintImage(image, new Extent(10, 5));
        },
        ["LinkToUrl"] = surface => surface.LinkToUrl("https://example.com", Offset.Zero, new Extent(10, 10)),
        ["LinkToDestination"] = surface => surface.LinkToDestination("target", Offset.Zero, new Extent(10, 10)),
        ["NameDestination"] = surface => surface.NameDestination("target", Offset.Zero)
    };

    /// <summary>Runs <paramref name="script"/>, which opens and closes its own pages, and returns the file.</summary>
    private static byte[] RenderDocument(Action<PdfSurface> script)
    {
        using MemoryStream stream = new MemoryStream();

        using (PdfDocumentWriter writer = new PdfDocumentWriter(stream))
        {
            using PdfSurface surface = new PdfSurface(writer, TypefaceLibrary.Shared.Shaper);
            script(surface);
            surface.Finish();
        }

        return stream.ToArray();
    }

    /// <summary>Draws a single square page.</summary>
    private static PdfDocument Render(Action<PdfSurface> draw) =>
        PdfDocument.Open(RenderDocument(surface =>
        {
            surface.BeginPage(new Extent(PageSide, PageSide));
            draw(surface);
            surface.EndPage();
        }));

    private static void AssertBounds(PdfRectangle? bounds, double left, double top, double width, double height)
    {
        Assert.True(bounds.HasValue, "The path has no extent.");
        Assert.Equal(left, bounds!.Value.Left, Tolerance);
        Assert.Equal(PageSide - top, bounds.Value.Top, Tolerance);
        Assert.Equal(width, bounds.Value.Width, Tolerance);
        Assert.Equal(height, bounds.Value.Height, Tolerance);
    }

    private static void AssertColour(Ink expected, IColor? actual)
    {
        Assert.NotNull(actual);
        (double red, double green, double blue) = actual!.ToRGBValues();
        (float expectedRed, float expectedGreen, float expectedBlue) = expected.ToRgb();

        Assert.Equal(expectedRed, red, 0.01);
        Assert.Equal(expectedGreen, green, 0.01);
        Assert.Equal(expectedBlue, blue, 0.01);
    }

    private static Letter LetterOf(Page page, string value) => Assert.Single(page.Letters, letter => letter.Value == value);

    // ---- Pages -------------------------------------------------------------------------------------------------

    [Fact]
    public void EachPageTakesTheSizeItWasBegunWith()
    {
        byte[] pdf = RenderDocument(surface =>
        {
            surface.BeginPage(new Extent(300, 150));
            surface.ShowText("First", new Offset(10, 50), Style, ReadingDirection.LeftToRight);
            surface.EndPage();

            surface.BeginPage(new Extent(120, 400));
            surface.ShowText("Second", new Offset(10, 50), Style, ReadingDirection.LeftToRight);
            surface.EndPage();
        });

        using PdfDocument parsed = PdfDocument.Open(pdf);

        Assert.Equal(2, parsed.NumberOfPages);
        Assert.Equal(300, parsed.GetPage(1).Width, Tolerance);
        Assert.Equal(150, parsed.GetPage(1).Height, Tolerance);
        Assert.Equal("First", parsed.GetPage(1).Text);
        Assert.Equal(120, parsed.GetPage(2).Width, Tolerance);
        Assert.Equal(400, parsed.GetPage(2).Height, Tolerance);
        Assert.Equal("Second", parsed.GetPage(2).Text);
    }

    [Theory]
    [InlineData("Save")]
    [InlineData("Restore")]
    [InlineData("MoveOrigin")]
    [InlineData("ScaleAxes")]
    [InlineData("RotateClockwise")]
    [InlineData("ClipRectangle")]
    [InlineData("FillRectangle")]
    [InlineData("DrawRoundedRectangle")]
    [InlineData("DrawLine")]
    [InlineData("DrawDashedLine")]
    [InlineData("Concatenate")]
    [InlineData("FillPath")]
    [InlineData("StrokePath")]
    [InlineData("ClipPath")]
    [InlineData("ShowText")]
    [InlineData("PaintImage")]
    [InlineData("LinkToUrl")]
    [InlineData("LinkToDestination")]
    [InlineData("NameDestination")]
    public void EveryOperationNeedsAnOpenPage(string operation)
    {
        RenderDocument(surface =>
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Operations[operation](surface));

            Assert.Contains("BeginPage", error.Message);

            // A document needs a page to be complete.
            surface.BeginPage(new Extent(PageSide, PageSide));
            surface.EndPage();
        });
    }

    [Fact]
    public void EndingAPageClosesItToFurtherDrawing()
    {
        byte[] pdf = RenderDocument(surface =>
        {
            surface.BeginPage(new Extent(PageSide, PageSide));
            surface.ShowText("Drawn", new Offset(10, 50), Style, ReadingDirection.LeftToRight);
            surface.EndPage();

            Assert.Throws<InvalidOperationException>(() => surface.ShowText("Late", new Offset(10, 90), Style, ReadingDirection.LeftToRight));
        });

        using PdfDocument parsed = PdfDocument.Open(pdf);

        Assert.Equal(1, parsed.NumberOfPages);
        Assert.Equal("Drawn", parsed.GetPage(1).Text);
    }

    // ---- Rectangles --------------------------------------------------------------------------------------------

    [Fact]
    public void FillRectangleFillsTheAreaInTheColourGiven()
    {
        using PdfDocument parsed = Render(surface =>
            surface.FillRectangle(new Offset(20, 30), new Extent(50, 40), Brick));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsFilled);
        Assert.False(path.IsStroked);
        AssertBounds(path.GetBoundingRectangle(), left: 20, top: 30, width: 50, height: 40);
        AssertColour(Brick, path.FillColor);
    }

    [Fact]
    public void APartiallyTransparentRectangleIsStillDrawn()
    {
        using PdfDocument parsed = Render(surface =>
            surface.FillRectangle(new Offset(20, 30), new Extent(50, 40), Brick.WithOpacity(1 / 255f)));

        Assert.Single(parsed.GetPage(1).Paths);
    }

    [Theory]
    [InlineData(50, 40, 0)]
    [InlineData(0, 40, 255)]
    [InlineData(50, 0, 255)]
    [InlineData(-50, 40, 255)]
    [InlineData(50, -40, 255)]
    public void FillRectangleDrawsNothingThatCouldNotBeSeen(float width, float height, byte alpha)
    {
        // A negative extent is not merely empty: Skia would normalise it and paint the mirror-image rectangle.
        using PdfDocument parsed = Render(surface =>
            surface.FillRectangle(new Offset(100, 100), new Extent(width, height), Brick.WithOpacity(alpha / 255f)));

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    [Fact]
    public void DrawRoundedRectangleFillsAShapeWithCurvedCorners()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawRoundedRectangle(new Offset(20, 30), new Extent(60, 40), Corners.All(10), Brick));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsFilled);
        Assert.False(path.IsStroked);
        Assert.Contains(path.SelectMany(subpath => subpath.Commands), command => command is PdfSubpath.CubicBezierCurve);
        AssertBounds(path.GetBoundingRectangle(), left: 20, top: 30, width: 60, height: 40);
        AssertColour(Brick, path.FillColor);

        // Each corner takes the radius off both edges it joins, so the long sides keep 60 - 2 * 10 straight.
        Assert.Equal(40, LongestStraightEdge(path), Tolerance);
    }

    [Fact]
    public void AnOversizedRadiusIsClampedToHalfTheShorterSide()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawRoundedRectangle(new Offset(20, 30), new Extent(60, 40), Corners.All(500), Brick));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        // At the clamp of 20 the short sides are all curve and the long sides keep 60 - 2 * 20 straight.
        Assert.Contains(path.SelectMany(subpath => subpath.Commands), command => command is PdfSubpath.CubicBezierCurve);
        AssertBounds(path.GetBoundingRectangle(), left: 20, top: 30, width: 60, height: 40);
        Assert.Equal(20, LongestStraightEdge(path), Tolerance);
    }

    private static double LongestStraightEdge(PdfPath path) =>
        path.SelectMany(subpath => subpath.Commands).OfType<PdfSubpath.Line>().Max(line => line.Length);

    [Fact]
    public void APositiveStrokeWidthOutlinesTheRoundedRectangleInstead()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawRoundedRectangle(new Offset(20, 30), new Extent(60, 40), Corners.All(10), Ocean, strokeWidth: 3));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsStroked);
        Assert.False(path.IsFilled);
        Assert.Equal(3, path.LineWidth, 0.01);
        AssertColour(Ocean, path.StrokeColor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AStrokeWidthOfZeroOrLessFillsTheRoundedRectangle(float strokeWidth)
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawRoundedRectangle(new Offset(20, 30), new Extent(60, 40), Corners.All(10), Brick, strokeWidth));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsFilled);
        Assert.False(path.IsStroked);
    }

    [Theory]
    [InlineData(0, 10, 10, 10)]
    [InlineData(10, 0, 10, 10)]
    [InlineData(10, 10, 0, 10)]
    [InlineData(10, 10, 10, 0)]
    public void ACornerWithNoRadiusStaysSquareWhileTheOthersRound(float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawRoundedRectangle(new Offset(20, 30), new Extent(60, 40), new Corners(topLeft, topRight, bottomRight, bottomLeft), Brick));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.Equal(3, path.SelectMany(subpath => subpath.Commands).Count(command => command is PdfSubpath.CubicBezierCurve));
        AssertBounds(path.GetBoundingRectangle(), left: 20, top: 30, width: 60, height: 40);

        // The square corner takes nothing off either long side it joins, so one of them runs 60 - 10 straight.
        Assert.Equal(50, LongestStraightEdge(path), Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8)]
    public void ARadiusOfZeroOrLessDrawsSquareCorners(float radius)
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawRoundedRectangle(new Offset(20, 30), new Extent(60, 40), Corners.All(radius), Brick));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.DoesNotContain(path.SelectMany(subpath => subpath.Commands), command => command is PdfSubpath.CubicBezierCurve);
        AssertBounds(path.GetBoundingRectangle(), left: 20, top: 30, width: 60, height: 40);
    }

    [Theory]
    [InlineData(60, 40, 0)]
    [InlineData(0, 40, 255)]
    [InlineData(60, 0, 255)]
    [InlineData(-60, 40, 255)]
    [InlineData(60, -40, 255)]
    public void DrawRoundedRectangleDrawsNothingThatCouldNotBeSeen(float width, float height, byte alpha)
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawRoundedRectangle(new Offset(100, 100), new Extent(width, height), Corners.All(5), Brick.WithOpacity(alpha / 255f)));

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    // ---- Lines -------------------------------------------------------------------------------------------------

    [Fact]
    public void DrawLineStrokesBetweenThePointsAtTheThicknessGiven()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 2.5f, Ocean));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsStroked);
        Assert.False(path.IsFilled);
        Assert.Equal(2.5, path.LineWidth, 0.01);
        AssertBounds(path.GetBoundingRectangle(), left: 10, top: 20, width: 100, height: 0);
        AssertColour(Ocean, path.StrokeColor);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 255)]
    [InlineData(-1, 255)]
    public void DrawLineDrawsNothingThatCouldNotBeSeen(float thickness, byte alpha)
    {
        // A zero stroke width is Skia's hairline, which would still paint a one-device-pixel line.
        using PdfDocument parsed = Render(surface =>
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), thickness, Ocean.WithOpacity(alpha / 255f)));

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    [Fact]
    public void ADoubleStrokeIsTwoLinesOfTheWeightEitherSideOfTheLine()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 3, Ocean, StrokeStyle.Double));

        List<PdfPath> paths = parsed.GetPage(1).Paths.ToList();

        Assert.Equal(2, paths.Count);
        Assert.All(paths, path => Assert.Equal(3, path.LineWidth, 0.01));
        Assert.All(paths, path => AssertColour(Ocean, path.StrokeColor));
        AssertBounds(paths[0].GetBoundingRectangle(), left: 10, top: 23, width: 100, height: 0);
        AssertBounds(paths[1].GetBoundingRectangle(), left: 10, top: 17, width: 100, height: 0);
    }

    [Fact]
    public void ADottedStrokeIsRoundDotsTwiceTheWeightApart()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 2, Ocean, StrokeStyle.Dotted));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.Equal(LineCapStyle.Round, path.LineCapStyle);
        Assert.Equal([0d, 4d], path.LineDashPattern!.Value.Array);
        Assert.Equal(2, path.LineWidth, 0.01);
    }

    [Fact]
    public void ADashedStrokeIsDashesThreeTimesTheWeightWithGapsOfTwice()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 2, Ocean, StrokeStyle.Dashed));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.Equal(LineCapStyle.Butt, path.LineCapStyle);
        Assert.Equal([6d, 4d], path.LineDashPattern!.Value.Array);
    }

    [Fact]
    public void ADashedLineTakesThePatternGiven()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawDashedLine(new Offset(10, 20), new Offset(110, 20), 2, Ocean, [5, 1, 0.5f, 1]));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsStroked);
        Assert.Equal(2, path.LineWidth, 0.01);
        Assert.Equal([5d, 1d, 0.5d, 1d], path.LineDashPattern!.Value.Array);
        AssertBounds(path.GetBoundingRectangle(), left: 10, top: 20, width: 100, height: 0);
        AssertColour(Ocean, path.StrokeColor);
    }

    [Fact]
    public void AStrokeDrawnAfterADashedLineIsSolid()
    {
        using PdfDocument parsed = Render(surface =>
        {
            surface.DrawDashedLine(new Offset(10, 20), new Offset(110, 20), 2, Ocean, [4, 2]);
            surface.DrawLine(new Offset(10, 40), new Offset(110, 40), 2, Ocean);
        });

        Assert.Empty(parsed.GetPage(1).Paths[1].LineDashPattern?.Array ?? []);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 255)]
    [InlineData(-1, 255)]
    public void ADashedLineDrawsNothingThatCouldNotBeSeen(float thickness, byte alpha)
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawDashedLine(new Offset(10, 20), new Offset(110, 20), thickness, Ocean.WithOpacity(alpha / 255f), [4, 2]));

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    [Theory]
    [InlineData(StrokeStyle.Dotted)]
    [InlineData(StrokeStyle.Dashed)]
    public void AStrokeDrawnAfterADottedOrDashedOneIsSolid(StrokeStyle first)
    {
        using PdfDocument parsed = Render(surface =>
        {
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 2, Ocean, first);
            surface.DrawLine(new Offset(10, 40), new Offset(110, 40), 2, Ocean);
        });

        PdfPath solid = parsed.GetPage(1).Paths[1];

        Assert.Equal(LineCapStyle.Butt, solid.LineCapStyle);
        Assert.Empty(solid.LineDashPattern?.Array ?? []);
        Assert.Equal(2, solid.LineWidth, 0.01);
    }

    [Fact]
    public void AWavyStrokeIsOneCurvedPathAlongTheLine()
    {
        using PdfDocument parsed = Render(surface =>
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 2, Ocean, StrokeStyle.Wavy));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);
        List<PdfSubpath.CubicBezierCurve> arches = path.SelectMany(subpath => subpath.Commands).OfType<PdfSubpath.CubicBezierCurve>().ToList();

        // A hundred points in half-waves four points long.
        Assert.Equal(25, arches.Count);
        Assert.True(path.IsStroked);
        Assert.Equal(2, path.LineWidth, 0.01);
        Assert.Equal(10, arches[0].StartPoint.X, Tolerance);
        Assert.Equal(110, arches[^1].EndPoint.X, Tolerance);
        Assert.Equal(PageSide - 20, arches[^1].EndPoint.Y, Tolerance);
    }

    [Theory]
    [InlineData(StrokeStyle.Double)]
    [InlineData(StrokeStyle.Dotted)]
    [InlineData(StrokeStyle.Dashed)]
    [InlineData(StrokeStyle.Wavy)]
    public void AStyledStrokeDrawsNothingThatCouldNotBeSeen(StrokeStyle style)
    {
        using PdfDocument parsed = Render(surface =>
        {
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 0, Ocean, style);
            surface.DrawLine(new Offset(10, 20), new Offset(110, 20), 2, Ocean.WithOpacity(0), style);
        });

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    // ---- Text --------------------------------------------------------------------------------------------------

    [Fact]
    public void ShowTextStartsOnTheBaselineAtThePositionGiven()
    {
        using PdfDocument parsed = Render(surface =>
            surface.ShowText("Baseline", new Offset(40, 120), Style.WithInk(Brick), ReadingDirection.LeftToRight));

        Page page = parsed.GetPage(1);
        Letter first = page.Letters[0];

        Assert.Equal("Baseline", page.Text);
        Assert.Equal(40, first.StartBaseLine.X, Tolerance);
        Assert.Equal(PageSide - 120, first.StartBaseLine.Y, Tolerance);
        Assert.Equal(20, first.PointSize, Tolerance);
        AssertColour(Brick, first.Color);
    }

    [Theory]
    [InlineData(null, 255)]
    [InlineData("", 255)]
    [InlineData("Invisible", 0)]
    public void ShowTextDrawsNothingThatCouldNotBeSeen(string? text, byte alpha)
    {
        using PdfDocument parsed = Render(surface =>
            surface.ShowText(text!, new Offset(40, 120), Style.WithInk(Brick.WithOpacity(alpha / 255f)), ReadingDirection.LeftToRight));

        Assert.Empty(parsed.GetPage(1).Letters);
    }

    [Fact]
    public void RunsInFallbackFontsFollowOnWithoutAGap()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(TypefaceLibrary.Shared.Shaper);

        using PdfDocument parsed = Render(surface => surface.ShowText("Hello世界", new Offset(40, 120), Style, ReadingDirection.LeftToRight));
        Page page = parsed.GetPage(1);

        Assert.Equal(40 + measurer.MeasureWidth("Hello", Style), LetterOf(page, "世").StartBaseLine.X, Tolerance);
        Assert.Equal(40 + measurer.MeasureWidth("Hello世", Style), LetterOf(page, "界").StartBaseLine.X, Tolerance);
    }

    [Fact]
    public void WordSpacingMovesTheWordsAfterEachSpace()
    {
        TypeStyle spaced = Style.WithWordSpacing(12);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(TypefaceLibrary.Shared.Shaper);

        using PdfDocument parsed = Render(surface => surface.ShowText("A B C", new Offset(20, 120), spaced, ReadingDirection.LeftToRight));
        Page page = parsed.GetPage(1);

        Assert.Equal(20 + measurer.MeasureWidth("A ", spaced), LetterOf(page, "B").StartBaseLine.X, Tolerance);
        Assert.Equal(20 + measurer.MeasureWidth("A B ", spaced), LetterOf(page, "C").StartBaseLine.X, Tolerance);
    }

    [Fact]
    public void TrackingSeparatesCharactersButDoesNotIndentTheFirst()
    {
        TypeStyle spaced = Style.WithTracking(6);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(TypefaceLibrary.Shared.Shaper);

        using PdfDocument parsed = Render(surface => surface.ShowText("ABCD", new Offset(40, 120), spaced, ReadingDirection.LeftToRight));
        IReadOnlyList<Letter> letters = parsed.GetPage(1).Letters;

        Assert.Equal("ABCD", string.Concat(letters.Select(letter => letter.Value)));
        Assert.Equal(40, letters[0].StartBaseLine.X, Tolerance);

        for (int index = 1; index < letters.Count; index++)
            Assert.Equal(6, letters[index].StartBaseLine.X - letters[index - 1].EndBaseLine.X, Tolerance);

        // What is drawn must occupy exactly the width the layout engine reserved for it.
        Assert.Equal(40 + measurer.MeasureWidth("ABCD", spaced), letters[3].EndBaseLine.X, Tolerance);
    }

    [Fact]
    public void TrackingLeavesAMarkOnItsLetter()
    {
        // Without ccmp, Noto Sans sets e and a combining acute as two glyphs; the acute takes no room of its own and
        // no tracking comes between it and the e, so the b after it lands as though it were not there.
        TypeStyle spaced = Style.WithFeature("ccmp", 0).WithTracking(6);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(TypefaceLibrary.Shared.Shaper);

        using PdfDocument parsed = Render(surface => surface.ShowText("aéb", new Offset(40, 120), spaced, ReadingDirection.LeftToRight));
        Page page = parsed.GetPage(1);

        Assert.Equal(40 + measurer.MeasureWidth("ae", spaced) + 6, LetterOf(page, "b").StartBaseLine.X, Tolerance);
        Assert.Equal(40 + measurer.MeasureWidth("aé", spaced) + 6, LetterOf(page, "b").StartBaseLine.X, Tolerance);
    }

    [Fact]
    public void TrackingKeepsASurrogatePairWholeAndCarriesAcrossFontRuns()
    {
        // The pair falls outside Arial, so it is also a separate font run: the gap before "B" proves the spacing
        // count carries on across runs rather than restarting in each.
        TypeStyle spaced = Style.WithTracking(6);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(TypefaceLibrary.Shared.Shaper);

        using PdfDocument parsed = Render(surface => surface.ShowText($"A{MathBoldA}B", new Offset(40, 120), spaced, ReadingDirection.LeftToRight));
        Page page = parsed.GetPage(1);

        // Split halves would each be an unmapped fragment and cost a second gap; whole, the pair is one glyph.
        Assert.Equal(3, page.Letters.Count);
        Assert.Equal(40 + measurer.MeasureWidth($"A{MathBoldA}", spaced) + 6, LetterOf(page, "B").StartBaseLine.X, Tolerance);
    }

    // ---- Images ------------------------------------------------------------------------------------------------

    [Fact]
    public void PaintImagePlacesTheImageAtTheOriginAtTheSizeGiven()
    {
        RasterImage image = RasterImage.FromBytes(TestImages.Png(64, 32));

        using PdfDocument parsed = Render(surface =>
        {
            surface.MoveOrigin(new Offset(30, 40));
            surface.PaintImage(image, new Extent(120, 60));
        });

        IPdfImage placed = Assert.Single(parsed.GetPage(1).GetImages());

        Assert.Equal(64, placed.WidthInSamples);
        Assert.Equal(32, placed.HeightInSamples);
        Assert.Equal(30, placed.BoundingBox.Left, Tolerance);
        Assert.Equal(PageSide - 40, placed.BoundingBox.Top, Tolerance);
        Assert.Equal(120, placed.BoundingBox.Width, Tolerance);
        Assert.Equal(60, placed.BoundingBox.Height, Tolerance);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(120, 0)]
    [InlineData(-120, 60)]
    [InlineData(120, -60)]
    public void PaintImageDrawsNothingIntoAnEmptyArea(float width, float height)
    {
        RasterImage image = RasterImage.FromBytes(TestImages.Png(64, 32));

        using PdfDocument parsed = Render(surface => surface.PaintImage(image, new Extent(width, height)));

        Assert.Empty(parsed.GetPage(1).GetImages());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnImageIsOpaqueAfterATranslucentFill(bool gradient)
    {
        // An image is painted with the fill's constant alpha, so one drawn in the same state as a translucent fill
        // would take on that fill's opacity; the raster surface draws it opaque.
        RasterImage image = RasterImage.FromBytes(TestImages.Png(4, 2));
        VectorPath square = new VectorPath().AddRectangle(0, 0, 5, 5);

        using PdfDocument parsed = Render(surface =>
        {
            if (gradient)
            {
                surface.BeginGradient(Gradient.Across(Brick.WithOpacity(0.3f), Ocean.WithOpacity(0.3f)), Offset.Zero, new Extent(5, 5));
                surface.FillPath(square, Brick, FillRule.NonZero);
                surface.EndGradient();
            }
            else
            {
                surface.FillRectangle(Offset.Zero, new Extent(5, 5), Ocean.WithOpacity(0.3f));
            }

            surface.StrokePath(square, Brick.WithOpacity(0.6f), new LineStyle(1));
            surface.PaintImage(image, new Extent(10, 5));
        });

        Page page = parsed.GetPage(1);
        List<UglyToad.PdfPig.Graphics.Operations.IGraphicsStateOperation> operations = page.Operations.ToList();
        int painted = operations.FindIndex(operation => operation is UglyToad.PdfPig.Graphics.Operations.InvokeNamedXObject);
        UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState.SetGraphicsStateParametersFromDictionary state = operations
            .Take(painted)
            .OfType<UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState.SetGraphicsStateParametersFromDictionary>()
            .Last();

        DictionaryToken resources = Resolve<DictionaryToken>(parsed, page.Dictionary.Data["Resources"]);
        DictionaryToken states = Resolve<DictionaryToken>(parsed, resources.Data["ExtGState"]);
        DictionaryToken opacity = Resolve<DictionaryToken>(parsed, states.Data[state.Name.Data]);

        Assert.Equal(1, Resolve<NumericToken>(parsed, opacity.Data["ca"]).Double);

        // The stroke's opacity is left as it was.
        Assert.Equal(0.6, Resolve<NumericToken>(parsed, opacity.Data["CA"]).Double, 0.001);
    }

    private static T Resolve<T>(PdfDocument parsed, IToken token)
        where T : IToken =>
        token is IndirectReferenceToken reference ? (T)parsed.Structure.GetObject(reference.Data).Data : (T)token;

    [Fact]
    public void AGradientThatCannotBePlacedPaintsNothing()
    {
        VectorPath square = new VectorPath().AddRectangle(0, 0, 5, 5);

        // Each scale is written as it is; together they reach beyond what a pattern's matrix can hold.
        using PdfDocument parsed = Render(surface =>
        {
            surface.ScaleAxes(1e10f, 1e10f);
            surface.ScaleAxes(1e10f, 1e10f);
            surface.BeginGradient(Gradient.Across(Brick, Ocean), Offset.Zero, new Extent(5, 5));
            surface.FillPath(square, Brick, FillRule.NonZero);
            surface.StrokePath(square, Brick, new LineStyle(1));
            surface.EndGradient();
            surface.FillPath(square, Brick, FillRule.NonZero);
        });

        Assert.Single(parsed.GetPage(1).Paths);
    }

    [Fact]
    public void PaintImageRejectsAnImageItDidNotDecode()
    {
        using PdfDocument parsed = Render(surface =>
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => surface.PaintImage(new ForeignImage(), new Extent(40, 20)));

            Assert.Equal("image", error.ParamName);
            Assert.Contains(nameof(RasterImage), error.Message);
        });

        Assert.Empty(parsed.GetPage(1).GetImages());
    }

    // ---- Links and destinations --------------------------------------------------------------------------------

    [Fact]
    public void LinkToUrlMakesTheAreaAtTheOriginClickable()
    {
        using PdfDocument parsed = Render(surface =>
        {
            surface.MoveOrigin(new Offset(20, 30));
            surface.LinkToUrl("https://example.com/report", Offset.Zero, new Extent(80, 15));
        });

        Annotation link = Assert.Single(parsed.GetPage(1).GetAnnotations());

        Assert.Equal(AnnotationType.Link, link.Type);
        Assert.Equal("https://example.com/report", Assert.IsType<UriAction>(link.Action).Uri);
        Assert.Equal(20, link.Rectangle.Left, Tolerance);
        Assert.Equal(PageSide - 30, link.Rectangle.Top, Tolerance);
        Assert.Equal(80, link.Rectangle.Width, Tolerance);
        Assert.Equal(15, link.Rectangle.Height, Tolerance);
    }

    [Fact]
    public void ALinkAwayFromTheOriginIsPlacedThroughTheWholeTransform()
    {
        // Turned a quarter clockwise about (100, 100), the box 40 across and 10 down at (10, 0) stands 10 across and 40
        // down, with its corners from x 90 to 100 and y 110 to 150 on the page.
        using PdfDocument parsed = Render(surface =>
        {
            surface.MoveOrigin(new Offset(100, 100));
            surface.RotateClockwise(90);
            surface.LinkToUrl("https://example.com/turned", new Offset(10, 0), new Extent(40, 10));
        });

        Annotation link = Assert.Single(parsed.GetPage(1).GetAnnotations());

        Assert.Equal(90, link.Rectangle.Left, Tolerance);
        Assert.Equal(PageSide - 110, link.Rectangle.Top, Tolerance);
        Assert.Equal(10, link.Rectangle.Width, Tolerance);
        Assert.Equal(40, link.Rectangle.Height, Tolerance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnExternalLinkWithNoTargetIsNotDrawn(string? url)
    {
        using PdfDocument parsed = Render(surface => surface.LinkToUrl(url!, Offset.Zero, new Extent(80, 15)));

        Assert.Empty(parsed.GetPage(1).GetAnnotations());
    }

    [Fact]
    public void AnInternalLinkJumpsToWhereItsDestinationWasDrawn()
    {
        byte[] pdf = RenderDocument(surface =>
        {
            surface.BeginPage(new Extent(PageSide, PageSide));
            surface.MoveOrigin(new Offset(10, 10));
            surface.LinkToDestination("appendix", Offset.Zero, new Extent(60, 12));
            surface.EndPage();

            surface.BeginPage(new Extent(PageSide, PageSide));
            surface.MoveOrigin(new Offset(0, 50));
            surface.NameDestination("appendix", Offset.Zero);
            surface.EndPage();
        });

        using PdfDocument parsed = PdfDocument.Open(pdf);
        Annotation link = Assert.Single(parsed.GetPage(1).GetAnnotations());
        GoToAction jump = Assert.IsType<GoToAction>(link.Action);

        Assert.Equal(10, link.Rectangle.Left, Tolerance);
        Assert.Equal(PageSide - 10, link.Rectangle.Top, Tolerance);
        Assert.Equal(60, link.Rectangle.Width, Tolerance);
        Assert.Equal(2, jump.Destination.PageNumber);
        Assert.Equal(PageSide - 50, jump.Destination.Coordinates.Top!.Value, Tolerance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnInternalLinkWithNoTargetIsNotDrawn(string? destination)
    {
        using PdfDocument parsed = Render(surface =>
        {
            surface.LinkToDestination(destination!, Offset.Zero, new Extent(60, 12));
            surface.NameDestination("elsewhere", Offset.Zero);
        });

        Assert.Empty(parsed.GetPage(1).GetAnnotations());
    }

    [Fact]
    public void NameDestinationRegistersANamedDestination()
    {
        using PdfDocument parsed = Render(surface => surface.NameDestination("chapter-one", Offset.Zero));

        // Where it points is checked through a link that resolves it, in AnInternalLinkJumpsToWhereItsDestinationWasDrawn.
        Assert.True(parsed.Structure.Catalog.CatalogDictionary.ContainsKey(NameToken.Create("Names")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ADestinationWithNoNameIsNotRegistered(string? name)
    {
        using PdfDocument parsed = Render(surface => surface.NameDestination(name!, Offset.Zero));

        Assert.False(parsed.Structure.Catalog.CatalogDictionary.ContainsKey(NameToken.Create("Names")));
    }

    // ---- Tagging -----------------------------------------------------------------------------------------------

    /// <summary>A document and a paragraph in it, for a tagged surface to be told what it draws belongs to.</summary>
    private static (StructureElement Document, StructureElement Paragraph) Structure()
    {
        StructureElement document = new StructureElement("Document", null);
        StructureElement paragraph = new StructureElement("P", document);
        document.Kids.Add(paragraph);
        return (document, paragraph);
    }

    /// <summary>Draws a single square page on a surface writing the structure, and returns the file uncompressed.</summary>
    private static string RenderTagged(Action<PdfSurface> draw)
    {
        using MemoryStream stream = new MemoryStream();

        using (PdfDocumentWriter writer = new PdfDocumentWriter(stream, new PdfWriterOptions { CompressionLevel = CompressionLevel.NoCompression }))
        {
            using PdfSurface surface = new PdfSurface(writer, TypefaceLibrary.Shared.Shaper, new PdfExportOptions { Tagged = true });
            surface.BeginPage(new Extent(PageSide, PageSide));
            draw(surface);
            surface.EndPage();
            surface.Finish();
        }

        return Encoding.Latin1.GetString(stream.ToArray());
    }

    [Fact]
    public void ATaggedPageOfDecorationAloneLeavesTheStructureOut()
    {
        string pdf = RenderTagged(surface =>
        {
            surface.Tag(null);
            surface.FillRectangle(new Offset(20, 30), new Extent(60, 40), Brick);
        });

        Assert.Matches(@"/Artifact\s+BMC", pdf);
        Assert.Matches(@"/Tabs\s*/S\b", pdf);
        Assert.DoesNotContain("/StructParents", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/StructTreeRoot", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void DecorationBeforeTheFirstElementStaysDecorationAndTheTreeGrowsFromThatElementsDocument()
    {
        (_, StructureElement paragraph) = Structure();

        string pdf = RenderTagged(surface =>
        {
            surface.Tag(null);
            surface.FillRectangle(new Offset(20, 30), new Extent(60, 40), Brick);
            surface.Tag(paragraph);
            surface.ShowText("Text", new Offset(10, 120), Style, ReadingDirection.LeftToRight);
        });

        Assert.Matches(@"/Artifact\s+BMC[\s\S]*/P\s*<<\s*/MCID 0\s*>>\s*BDC", pdf);
        Assert.Matches(@"/StructTreeRoot \d+ 0 R", pdf);
        Assert.Matches(@"/S\s*/Document", pdf);
        Assert.Matches(@"/StructParents 0\b", pdf);
    }

    [Fact]
    public void ALinkDrawnInsideAnotherElementIsALinkElementWithinIt()
    {
        (_, StructureElement paragraph) = Structure();

        string pdf = RenderTagged(surface =>
        {
            surface.Tag(paragraph);
            surface.LinkToUrl("https://example.com/", Offset.Zero, new Extent(80, 15));
        });

        StructureElement link = Assert.IsType<StructureElement>(Assert.Single(paragraph.Kids));
        Assert.Equal("Link", link.Role);
        Assert.Single(link.Kids);
        Assert.Matches(@"/S\s*/Link\b", pdf);
        Assert.Matches(@"/Subtype\s*/Link[\s\S]*?/StructParent 0\b", pdf);
    }

    // ---- Transforms --------------------------------------------------------------------------------------------

    [Fact]
    public void RestoreUndoesTheTransformAppliedSinceSave()
    {
        using PdfDocument parsed = Render(surface =>
        {
            surface.Save();
            surface.MoveOrigin(new Offset(50, 60));
            surface.ShowText("M", new Offset(10, 40), Style, ReadingDirection.LeftToRight);
            surface.Restore();
            surface.ShowText("H", new Offset(10, 40), Style, ReadingDirection.LeftToRight);
        });

        Page page = parsed.GetPage(1);

        Assert.Equal(60, LetterOf(page, "M").StartBaseLine.X, Tolerance);
        Assert.Equal(PageSide - 100, LetterOf(page, "M").StartBaseLine.Y, Tolerance);
        Assert.Equal(10, LetterOf(page, "H").StartBaseLine.X, Tolerance);
        Assert.Equal(PageSide - 40, LetterOf(page, "H").StartBaseLine.Y, Tolerance);
    }

    [Fact]
    public void ScaleStretchesEachAxisByItsOwnFactor()
    {
        using PdfDocument parsed = Render(surface =>
        {
            surface.ScaleAxes(2, 3);
            surface.FillRectangle(new Offset(10, 10), new Extent(20, 20), Brick);
        });

        AssertBounds(Assert.Single(parsed.GetPage(1).Paths).GetBoundingRectangle(), left: 20, top: 30, width: 40, height: 60);
    }

    [Fact]
    public void RotateTurnsClockwiseAboutTheOrigin()
    {
        // A bar running right from the origin must end up hanging downwards from it, to the left of the X axis.
        using PdfDocument parsed = Render(surface =>
        {
            surface.MoveOrigin(new Offset(100, 100));
            surface.RotateClockwise(90);
            surface.FillRectangle(Offset.Zero, new Extent(40, 10), Brick);
        });

        AssertBounds(Assert.Single(parsed.GetPage(1).Paths).GetBoundingRectangle(), left: 90, top: 100, width: 10, height: 40);
    }

    [Fact]
    public void RotateTakesAnyAngle()
    {
        // An eighth of a turn is written as its own matrix, inside the page's Y-down flip, so it turns clockwise on
        // the page. PdfPig bounds a rectangle by two of its corners, so the matrix is what is checked.
        using PdfDocument parsed = Render(surface =>
        {
            surface.MoveOrigin(new Offset(100, 100));
            surface.RotateClockwise(45);
            surface.FillRectangle(Offset.Zero, new Extent(20, 20), Brick);
        });

        string content = System.Text.Encoding.ASCII.GetString(parsed.GetPage(1).Operations
            .Select(operation => { using MemoryStream buffer = new MemoryStream(); operation.Write(buffer); return buffer.ToArray(); })
            .SelectMany(bytes => bytes.Append((byte)'\n'))
            .ToArray());

        Assert.Matches(@"1 0 0 1 100 100 cm\s+0\.70711 0\.70711 -0\.70711 0\.70711 0 0 cm\s+", content);
    }

    [Fact]
    public void ClipRectangleConfinesDrawingToAnAreaAtTheCurrentOrigin()
    {
        // Clipped content stays in the file, hidden, so the clip itself is what is checked: a rectangle at the
        // current origin, set as the clip and not painted, after the translation that moved it. Rendering it is
        // the conformance suite's job.
        using PdfDocument parsed = Render(surface =>
        {
            surface.MoveOrigin(new Offset(100, 100));
            surface.ClipRectangle(new Extent(90, 50));
            surface.ShowText("Inside", new Offset(5, 30), Style, ReadingDirection.LeftToRight);
        });

        string content = System.Text.Encoding.ASCII.GetString(parsed.GetPage(1).Operations
            .Select(operation => { using MemoryStream buffer = new MemoryStream(); operation.Write(buffer); return buffer.ToArray(); })
            .SelectMany(bytes => bytes.Append((byte)'\n'))
            .ToArray());

        Assert.Matches(@"1 0 0 1 100 100 cm\s+0 0 90 50 re\s+W\s+n", content);
    }

    // ---- Disposal ----------------------------------------------------------------------------------------------

    [Fact]
    public void DisposeEndsAPageLeftOpen()
    {
        using MemoryStream stream = new MemoryStream();

        using (PdfDocumentWriter writer = new PdfDocumentWriter(stream))
        {
            PdfSurface surface = new PdfSurface(writer, TypefaceLibrary.Shared.Shaper);
            surface.BeginPage(new Extent(PageSide, PageSide));
            surface.ShowText("Unfinished", new Offset(10, 50), Style, ReadingDirection.LeftToRight);

            surface.Dispose();

            Assert.Throws<InvalidOperationException>(() => surface.ShowText("Late", new Offset(10, 90), Style, ReadingDirection.LeftToRight));
            surface.Finish();
        }

        using PdfDocument parsed = PdfDocument.Open(stream.ToArray());

        Assert.Equal(1, parsed.NumberOfPages);
        Assert.Equal("Unfinished", parsed.GetPage(1).Text);
    }

    [Fact]
    public void DisposeLeavesAWriterWithNoPageOpenAlone()
    {
        using MemoryStream stream = new MemoryStream();

        using (PdfDocumentWriter writer = new PdfDocumentWriter(stream))
        {
            PdfSurface surface = new PdfSurface(writer, TypefaceLibrary.Shared.Shaper);
            surface.BeginPage(new Extent(PageSide, PageSide));
            surface.EndPage();

            surface.Dispose();
            surface.Dispose();
            surface.Finish();
        }

        using PdfDocument parsed = PdfDocument.Open(stream.ToArray());
        Assert.Equal(1, parsed.NumberOfPages);
    }

    [Fact]
    public void DisposeSwallowsAFailureToEndAPageItWasUnwindingFrom()
    {
        // A page left with its graphics state still saved cannot be ended cleanly; Dispose runs while an earlier
        // failure unwinds, and must not replace that failure with its own.
        using MemoryStream stream = new MemoryStream();
        using PdfDocumentWriter writer = new PdfDocumentWriter(stream);
        PdfSurface surface = new PdfSurface(writer, TypefaceLibrary.Shared.Shaper);
        surface.BeginPage(new Extent(PageSide, PageSide));
        surface.Save();

        surface.Dispose();

        Assert.Throws<InvalidOperationException>(() => surface.ShowText("Late", new Offset(10, 90), Style, ReadingDirection.LeftToRight));
    }

    [Fact]
    public void ALongRunIsSetWhole()
    {
        // Longer than the surface's first buffer of glyph codes, with no kerning pair to break it up.
        string text = new string('l', 300);

        using PdfDocument parsed = Render(surface => surface.ShowText(text, new Offset(4, 50), Style.WithPointSize(2), ReadingDirection.LeftToRight));

        Assert.Equal(text, parsed.GetPage(1).Text);
    }

    [Fact]
    public void BeginningAPageWhileOneIsOpenIsRefused()
    {
        RenderDocument(surface =>
        {
            surface.BeginPage(new Extent(PageSide, PageSide));

            Assert.Throws<InvalidOperationException>(() => surface.BeginPage(new Extent(PageSide, PageSide)));

            surface.EndPage();
        });
    }
}
