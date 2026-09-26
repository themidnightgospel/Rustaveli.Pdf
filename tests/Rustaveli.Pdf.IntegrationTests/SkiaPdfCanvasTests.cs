using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using Rustaveli.Pdf.Text;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Tokens;
using Color = Rustaveli.Pdf.Primitives.Color;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Drives the Skia canvas directly and reads the file back with an independent parser.
/// </summary>
/// <remarks>
/// Document-level tests reach the canvas only through what the layout engine happens to emit, which never
/// includes an empty link, a zero-width rule or a clip. The canvas is public, so those inputs are part of its
/// contract all the same. PDF puts the origin at the bottom-left with Y upwards, so every expected vertical
/// position here is the page height minus the canvas coordinate.
/// </remarks>
public class SkiaPdfCanvasTests
{
    private const float PageSide = 200f;
    private const double Tolerance = 0.5;

    private static readonly Color Brick = new Color(200, 40, 40);
    private static readonly Color Ocean = new Color(10, 120, 230);
    private static readonly TextStyle Style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(20);

    /// <summary>A character outside the Basic Multilingual Plane: one character, two UTF-16 code units.</summary>
    private const string MathBoldA = "\U0001D400";

    private static readonly Dictionary<string, Action<SkiaPdfCanvas>> Operations = new Dictionary<string, Action<SkiaPdfCanvas>>
    {
        ["Save"] = canvas => canvas.Save(),
        ["Restore"] = canvas => canvas.Restore(),
        ["Translate"] = canvas => canvas.Translate(new Position(5, 5)),
        ["Scale"] = canvas => canvas.Scale(2, 2),
        ["Rotate"] = canvas => canvas.Rotate(90),
        ["ClipRectangle"] = canvas => canvas.ClipRectangle(new Size(10, 10)),
        ["DrawRectangle"] = canvas => canvas.DrawRectangle(Position.Zero, new Size(10, 10), Brick),
        ["DrawRoundedRectangle"] = canvas => canvas.DrawRoundedRectangle(Position.Zero, new Size(10, 10), 2, Brick),
        ["DrawLine"] = canvas => canvas.DrawLine(Position.Zero, new Position(10, 0), 1, Brick),
        ["DrawText"] = canvas => canvas.DrawText("Text", new Position(10, 30), Style),
        ["DrawImage"] = canvas =>
        {
            using SkiaImage image = SkiaImage.FromBytes(TestImages.Png(4, 2));
            canvas.DrawImage(image, new Size(10, 5));
        },
        ["DrawExternalLink"] = canvas => canvas.DrawExternalLink("https://example.com", new Size(10, 10)),
        ["DrawInternalLink"] = canvas => canvas.DrawInternalLink("target", new Size(10, 10)),
        ["DrawDestination"] = canvas => canvas.DrawDestination("target")
    };

    private static SKDocumentPdfMetadata Metadata() => new SKDocumentPdfMetadata { RasterDpi = 72, EncodingQuality = 101 };

    /// <summary>Runs <paramref name="script"/>, which opens and closes its own pages, and returns the file.</summary>
    private static byte[] RenderDocument(Action<SkiaPdfCanvas> script)
    {
        using MemoryStream stream = new MemoryStream();

        using (SKDocument document = SKDocument.CreatePdf(stream, Metadata()))
        {
            using SkiaPdfCanvas canvas = new SkiaPdfCanvas(document, SkiaFontProvider.Shared);
            script(canvas);
            document.Close();
        }

        return stream.ToArray();
    }

    /// <summary>Draws a single square page.</summary>
    private static PdfDocument Render(Action<SkiaPdfCanvas> draw) =>
        PdfDocument.Open(RenderDocument(canvas =>
        {
            canvas.BeginPage(new Size(PageSide, PageSide));
            draw(canvas);
            canvas.EndPage();
        }));

    private static void AssertBounds(PdfRectangle? bounds, double left, double top, double width, double height)
    {
        Assert.True(bounds.HasValue, "The path has no extent.");
        Assert.Equal(left, bounds!.Value.Left, Tolerance);
        Assert.Equal(PageSide - top, bounds.Value.Top, Tolerance);
        Assert.Equal(width, bounds.Value.Width, Tolerance);
        Assert.Equal(height, bounds.Value.Height, Tolerance);
    }

    private static void AssertColour(Color expected, IColor? actual)
    {
        Assert.NotNull(actual);
        (double red, double green, double blue) = actual!.ToRGBValues();

        Assert.Equal(expected.Red / 255.0, red, 0.01);
        Assert.Equal(expected.Green / 255.0, green, 0.01);
        Assert.Equal(expected.Blue / 255.0, blue, 0.01);
    }

    private static Letter LetterOf(Page page, string value) => Assert.Single(page.Letters, letter => letter.Value == value);

    // ---- Pages -------------------------------------------------------------------------------------------------

    [Fact]
    public void EachPageTakesTheSizeItWasBegunWith()
    {
        byte[] pdf = RenderDocument(canvas =>
        {
            canvas.BeginPage(new Size(300, 150));
            canvas.DrawText("First", new Position(10, 50), Style);
            canvas.EndPage();

            canvas.BeginPage(new Size(120, 400));
            canvas.DrawText("Second", new Position(10, 50), Style);
            canvas.EndPage();
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
    [InlineData("Translate")]
    [InlineData("Scale")]
    [InlineData("Rotate")]
    [InlineData("ClipRectangle")]
    [InlineData("DrawRectangle")]
    [InlineData("DrawRoundedRectangle")]
    [InlineData("DrawLine")]
    [InlineData("DrawText")]
    [InlineData("DrawImage")]
    [InlineData("DrawExternalLink")]
    [InlineData("DrawInternalLink")]
    [InlineData("DrawDestination")]
    public void EveryOperationNeedsAnOpenPage(string operation)
    {
        RenderDocument(canvas =>
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Operations[operation](canvas));

            Assert.Contains("BeginPage", error.Message);
        });
    }

    [Fact]
    public void EndingAPageClosesItToFurtherDrawing()
    {
        byte[] pdf = RenderDocument(canvas =>
        {
            canvas.BeginPage(new Size(PageSide, PageSide));
            canvas.DrawText("Drawn", new Position(10, 50), Style);
            canvas.EndPage();

            Assert.Throws<InvalidOperationException>(() => canvas.DrawText("Late", new Position(10, 90), Style));
        });

        using PdfDocument parsed = PdfDocument.Open(pdf);

        Assert.Equal(1, parsed.NumberOfPages);
        Assert.Equal("Drawn", parsed.GetPage(1).Text);
    }

    // ---- Rectangles --------------------------------------------------------------------------------------------

    [Fact]
    public void DrawRectangleFillsTheAreaInTheColourGiven()
    {
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRectangle(new Position(20, 30), new Size(50, 40), Brick));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsFilled);
        Assert.False(path.IsStroked);
        AssertBounds(path.GetBoundingRectangle(), left: 20, top: 30, width: 50, height: 40);
        AssertColour(Brick, path.FillColor);
    }

    [Fact]
    public void APartiallyTransparentRectangleIsStillDrawn()
    {
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRectangle(new Position(20, 30), new Size(50, 40), Brick.WithAlpha(1)));

        Assert.Single(parsed.GetPage(1).Paths);
    }

    [Theory]
    [InlineData(50, 40, 0)]
    [InlineData(0, 40, 255)]
    [InlineData(50, 0, 255)]
    [InlineData(-50, 40, 255)]
    [InlineData(50, -40, 255)]
    public void DrawRectangleDrawsNothingThatCouldNotBeSeen(float width, float height, byte alpha)
    {
        // A negative extent is not merely empty: Skia would normalise it and paint the mirror-image rectangle.
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRectangle(new Position(100, 100), new Size(width, height), Brick.WithAlpha(alpha)));

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    [Fact]
    public void DrawRoundedRectangleFillsAShapeWithCurvedCorners()
    {
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRoundedRectangle(new Position(20, 30), new Size(60, 40), 10, Brick));

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
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRoundedRectangle(new Position(20, 30), new Size(60, 40), 500, Brick));

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
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRoundedRectangle(new Position(20, 30), new Size(60, 40), 10, Ocean, strokeWidth: 3));

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
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRoundedRectangle(new Position(20, 30), new Size(60, 40), 10, Brick, strokeWidth));

        PdfPath path = Assert.Single(parsed.GetPage(1).Paths);

        Assert.True(path.IsFilled);
        Assert.False(path.IsStroked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8)]
    public void ARadiusOfZeroOrLessDrawsSquareCorners(float radius)
    {
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRoundedRectangle(new Position(20, 30), new Size(60, 40), radius, Brick));

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
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawRoundedRectangle(new Position(100, 100), new Size(width, height), 5, Brick.WithAlpha(alpha)));

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    // ---- Lines -------------------------------------------------------------------------------------------------

    [Fact]
    public void DrawLineStrokesBetweenThePointsAtTheThicknessGiven()
    {
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawLine(new Position(10, 20), new Position(110, 20), 2.5f, Ocean));

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
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawLine(new Position(10, 20), new Position(110, 20), thickness, Ocean.WithAlpha(alpha)));

        Assert.Empty(parsed.GetPage(1).Paths);
    }

    // ---- Text --------------------------------------------------------------------------------------------------

    [Fact]
    public void DrawTextStartsOnTheBaselineAtThePositionGiven()
    {
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawText("Baseline", new Position(40, 120), Style.ColorOf(Brick)));

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
    public void DrawTextDrawsNothingThatCouldNotBeSeen(string? text, byte alpha)
    {
        using PdfDocument parsed = Render(canvas =>
            canvas.DrawText(text!, new Position(40, 120), Style.ColorOf(Brick.WithAlpha(alpha))));

        Assert.Empty(parsed.GetPage(1).Letters);
    }

    [Fact]
    public void RunsInFallbackFontsFollowOnWithoutAGap()
    {
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);

        using PdfDocument parsed = Render(canvas => canvas.DrawText("Hello世界", new Position(40, 120), Style));
        Page page = parsed.GetPage(1);

        Assert.Equal(40 + measurer.MeasureWidth("Hello", Style), LetterOf(page, "世").StartBaseLine.X, Tolerance);
        Assert.Equal(40 + measurer.MeasureWidth("Hello世", Style), LetterOf(page, "界").StartBaseLine.X, Tolerance);
    }

    [Fact]
    public void LetterSpacingSeparatesCharactersButDoesNotIndentTheFirst()
    {
        TextStyle spaced = Style.LetterSpacingOf(6);
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);

        using PdfDocument parsed = Render(canvas => canvas.DrawText("ABCD", new Position(40, 120), spaced));
        IReadOnlyList<Letter> letters = parsed.GetPage(1).Letters;

        Assert.Equal("ABCD", string.Concat(letters.Select(letter => letter.Value)));
        Assert.Equal(40, letters[0].StartBaseLine.X, Tolerance);

        for (int index = 1; index < letters.Count; index++)
            Assert.Equal(6, letters[index].StartBaseLine.X - letters[index - 1].EndBaseLine.X, Tolerance);

        // What is drawn must occupy exactly the width the layout engine reserved for it.
        Assert.Equal(40 + measurer.MeasureWidth("ABCD", spaced), letters[3].EndBaseLine.X, Tolerance);
    }

    [Fact]
    public void LetterSpacingKeepsASurrogatePairWholeAndCarriesAcrossFontRuns()
    {
        // The pair falls outside Arial, so it is also a separate font run: the gap before "B" proves the spacing
        // count carries on across runs rather than restarting in each.
        TextStyle spaced = Style.LetterSpacingOf(6);
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);

        using PdfDocument parsed = Render(canvas => canvas.DrawText($"A{MathBoldA}B", new Position(40, 120), spaced));
        Page page = parsed.GetPage(1);

        // Split halves would each be an unmapped fragment and cost a second gap; whole, the pair is one glyph.
        Assert.Equal(3, page.Letters.Count);
        Assert.Equal(40 + measurer.MeasureWidth($"A{MathBoldA}", spaced) + 6, LetterOf(page, "B").StartBaseLine.X, Tolerance);
    }

    // ---- Images ------------------------------------------------------------------------------------------------

    [Fact]
    public void DrawImagePlacesTheImageAtTheOriginAtTheSizeGiven()
    {
        using SkiaImage image = SkiaImage.FromBytes(TestImages.Png(64, 32));

        using PdfDocument parsed = Render(canvas =>
        {
            canvas.Translate(new Position(30, 40));
            canvas.DrawImage(image, new Size(120, 60));
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
    public void DrawImageDrawsNothingIntoAnEmptyArea(float width, float height)
    {
        using SkiaImage image = SkiaImage.FromBytes(TestImages.Png(64, 32));

        using PdfDocument parsed = Render(canvas => canvas.DrawImage(image, new Size(width, height)));

        Assert.Empty(parsed.GetPage(1).GetImages());
    }

    [Fact]
    public void DrawImageRejectsAnImageItDidNotDecode()
    {
        using PdfDocument parsed = Render(canvas =>
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => canvas.DrawImage(new ForeignImage(), new Size(40, 20)));

            Assert.Equal("image", error.ParamName);
            Assert.Contains(nameof(SkiaImage), error.Message);
        });

        Assert.Empty(parsed.GetPage(1).GetImages());
    }

    // ---- Links and destinations --------------------------------------------------------------------------------

    [Fact]
    public void DrawExternalLinkMakesTheAreaAtTheOriginClickable()
    {
        using PdfDocument parsed = Render(canvas =>
        {
            canvas.Translate(new Position(20, 30));
            canvas.DrawExternalLink("https://example.com/report", new Size(80, 15));
        });

        Annotation link = Assert.Single(parsed.GetPage(1).GetAnnotations());

        Assert.Equal(AnnotationType.Link, link.Type);
        Assert.Equal("https://example.com/report", Assert.IsType<UriAction>(link.Action).Uri);
        Assert.Equal(20, link.Rectangle.Left, Tolerance);
        Assert.Equal(PageSide - 30, link.Rectangle.Top, Tolerance);
        Assert.Equal(80, link.Rectangle.Width, Tolerance);
        Assert.Equal(15, link.Rectangle.Height, Tolerance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnExternalLinkWithNoTargetIsNotDrawn(string? url)
    {
        using PdfDocument parsed = Render(canvas => canvas.DrawExternalLink(url!, new Size(80, 15)));

        Assert.Empty(parsed.GetPage(1).GetAnnotations());
    }

    [Fact]
    public void AnInternalLinkJumpsToWhereItsDestinationWasDrawn()
    {
        byte[] pdf = RenderDocument(canvas =>
        {
            canvas.BeginPage(new Size(PageSide, PageSide));
            canvas.Translate(new Position(10, 10));
            canvas.DrawInternalLink("appendix", new Size(60, 12));
            canvas.EndPage();

            canvas.BeginPage(new Size(PageSide, PageSide));
            canvas.Translate(new Position(0, 50));
            canvas.DrawDestination("appendix");
            canvas.EndPage();
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
        using PdfDocument parsed = Render(canvas =>
        {
            canvas.DrawInternalLink(destination!, new Size(60, 12));
            canvas.DrawDestination("elsewhere");
        });

        Assert.Empty(parsed.GetPage(1).GetAnnotations());
    }

    [Fact]
    public void DrawDestinationRegistersANamedDestination()
    {
        using PdfDocument parsed = Render(canvas => canvas.DrawDestination("chapter-one"));

        Assert.True(parsed.Structure.Catalog.CatalogDictionary.ContainsKey(NameToken.Create("Dests")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ADestinationWithNoNameIsNotRegistered(string? name)
    {
        using PdfDocument parsed = Render(canvas => canvas.DrawDestination(name!));

        Assert.False(parsed.Structure.Catalog.CatalogDictionary.ContainsKey(NameToken.Create("Dests")));
    }

    // ---- Transforms --------------------------------------------------------------------------------------------

    [Fact]
    public void RestoreUndoesTheTransformAppliedSinceSave()
    {
        using PdfDocument parsed = Render(canvas =>
        {
            canvas.Save();
            canvas.Translate(new Position(50, 60));
            canvas.DrawText("M", new Position(10, 40), Style);
            canvas.Restore();
            canvas.DrawText("H", new Position(10, 40), Style);
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
        using PdfDocument parsed = Render(canvas =>
        {
            canvas.Scale(2, 3);
            canvas.DrawRectangle(new Position(10, 10), new Size(20, 20), Brick);
        });

        AssertBounds(Assert.Single(parsed.GetPage(1).Paths).GetBoundingRectangle(), left: 20, top: 30, width: 40, height: 60);
    }

    [Fact]
    public void RotateTurnsClockwiseAboutTheOrigin()
    {
        // A bar running right from the origin must end up hanging downwards from it, to the left of the X axis.
        using PdfDocument parsed = Render(canvas =>
        {
            canvas.Translate(new Position(100, 100));
            canvas.Rotate(90);
            canvas.DrawRectangle(Position.Zero, new Size(40, 10), Brick);
        });

        AssertBounds(Assert.Single(parsed.GetPage(1).Paths).GetBoundingRectangle(), left: 90, top: 100, width: 10, height: 40);
    }

    [Fact]
    public void ClipRectangleConfinesDrawingToAnAreaAtTheCurrentOrigin()
    {
        // The second word lies inside a clip anchored at the page corner, so it only disappears if the clip moved
        // with the translation.
        using PdfDocument parsed = Render(canvas =>
        {
            canvas.Translate(new Position(100, 100));
            canvas.ClipRectangle(new Size(50, 50));
            canvas.DrawText("Inside", new Position(5, 30), Style);
            canvas.DrawText("Outside", new Position(-90, -60), Style);
        });

        string text = parsed.GetPage(1).Text;

        Assert.Contains("Inside", text);
        Assert.DoesNotContain("Outside", text);
    }

    // ---- Disposal ----------------------------------------------------------------------------------------------

    [Fact]
    public void DisposeEndsAPageLeftOpen()
    {
        using MemoryStream stream = new MemoryStream();

        using (SKDocument document = SKDocument.CreatePdf(stream, Metadata()))
        {
            SkiaPdfCanvas canvas = new SkiaPdfCanvas(document, SkiaFontProvider.Shared);
            canvas.BeginPage(new Size(PageSide, PageSide));
            canvas.DrawText("Unfinished", new Position(10, 50), Style);

            canvas.Dispose();

            Assert.Throws<InvalidOperationException>(() => canvas.DrawText("Late", new Position(10, 90), Style));
            document.Close();
        }

        using PdfDocument parsed = PdfDocument.Open(stream.ToArray());

        Assert.Equal(1, parsed.NumberOfPages);
        Assert.Equal("Unfinished", parsed.GetPage(1).Text);
    }

    [Fact]
    public void DisposeDoesNotTouchADocumentWithNoPageOpen()
    {
        // The canvas only borrows the document. Once the owner has released it, any call into it is an access
        // violation that takes the process down rather than an exception, so the canvas must leave it alone.
        using MemoryStream stream = new MemoryStream();
        SKDocument document = SKDocument.CreatePdf(stream, Metadata());
        SkiaPdfCanvas canvas = new SkiaPdfCanvas(document, SkiaFontProvider.Shared);

        canvas.BeginPage(new Size(PageSide, PageSide));
        canvas.EndPage();
        document.Close();
        document.Dispose();

        canvas.Dispose();
        canvas.Dispose();

        using PdfDocument parsed = PdfDocument.Open(stream.ToArray());
        Assert.Equal(1, parsed.NumberOfPages);
    }
}
