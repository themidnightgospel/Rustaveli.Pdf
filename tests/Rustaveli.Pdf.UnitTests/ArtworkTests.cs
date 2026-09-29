namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Artwork drawn step by step, and placed in a frame as an image is.
/// </summary>
public class ArtworkTests
{
    private static readonly VectorPath Square = new VectorPath().AddRectangle(0, 0, 10, 10);

    private static List<DrawOperation> Draw(Artwork artwork, Extent? space = null, ImageFitting fit = ImageFitting.FitWidth) =>
        LayoutHarness.Draw(frame => frame.Artwork(artwork, fit), space ?? new Extent(100, 100)).Operations;

    [Fact]
    public void FillsStrokesAndTextAreDrawnInOrder()
    {
        TypeStyle style = TypeStyle.Default.WithPointSize(10);
        Artwork artwork = Artwork.Draw(100, 50, art =>
        {
            art.Fill(Square, TestInks.Red, FillRule.EvenOdd);
            art.Stroke(Square, TestInks.Black, new LineStyle(2, LineCap.Round));
            art.Text("Label", 5, 40, style);
        });

        List<DrawOperation> operations = Draw(artwork);

        PathOperation fill = Assert.IsType<PathOperation>(operations[0]);
        Assert.Equal((PathPainting.Fill, TestInks.Red, FillRule.EvenOdd), (fill.Painting, fill.Ink, fill.Rule));
        PathOperation stroke = Assert.IsType<PathOperation>(operations[1]);
        Assert.Equal((PathPainting.Stroke, new LineStyle(2, LineCap.Round)), (stroke.Painting, stroke.Style));
        Assert.Equal("Label", Assert.IsType<TextOperation>(operations[2]).Text);
    }

    [Fact]
    public void ArtworkIsScaledToItsFrame()
    {
        Artwork artwork = Artwork.Draw(50, 25, art => art.Fill(Square, TestInks.Red));

        // Twice as big, to fill the width.
        Assert.Equal(new Bounds(0, 0, 20, 20), Assert.IsType<PathOperation>(Draw(artwork)[0]).Bounds);
    }

    [Theory]
    [InlineData(ImageFitting.FitWidth, 100f, 50f)]
    [InlineData(ImageFitting.FitHeight, 60f, 30f)]
    [InlineData(ImageFitting.Proportionally, 60f, 30f)]
    [InlineData(ImageFitting.Stretch, 100f, 30f)]
    public void ArtworkIsFittedAsAnImageIs(ImageFitting fit, float width, float height)
    {
        Artwork artwork = Artwork.Draw(50, 25, art => { });
        Block root = LayoutHarness.Build(frame => frame.Artwork(artwork, fit));

        Assert.Equal(new Extent(width, height), LayoutHarness.Measure(root, new Extent(100, fit == ImageFitting.FitWidth ? 100 : 30)).Size);
    }

    [Fact]
    public void ArtworkThatDoesNotFitMovesOn()
    {
        Artwork artwork = Artwork.Draw(50, 25, art => art.Fill(Square, TestInks.Red));
        Block root = LayoutHarness.Build(frame => frame.Artwork(artwork));

        Assert.True(LayoutHarness.Measure(root, new Extent(100, 20)).IsDeferred);
        Assert.Empty(LayoutHarness.Draw(root, new Extent(100, 20)).Operations);
    }

    [Fact]
    public void TransformsApplyToWhatFollowsUntilRestored()
    {
        Artwork artwork = Artwork.Draw(100, 100, art =>
        {
            art.SaveState();
            art.Translate(10, 20);
            art.Scale(2, 3);
            art.Fill(Square, TestInks.Red);
            art.RestoreState();
            art.SaveState();
            art.Rotate(90);
            art.Fill(Square, TestInks.Red);
            art.RestoreState();
            art.Transform(1, 0, 1, 1, 0, 0);
            art.Fill(Square, TestInks.Red);
        });

        List<Bounds> bounds = Draw(artwork).OfType<PathOperation>().Select(operation => operation.Bounds).ToList();

        Assert.Equal(new Bounds(10, 20, 30, 50), bounds[0]);
        Approximately.Equal(new Offset(-10, 0), new Offset(bounds[1].Left, bounds[1].Top));
        Assert.Equal(new Bounds(0, 0, 20, 10), bounds[2]);
    }

    [Fact]
    public void AClipIsRecordedForWhatFollows()
    {
        Artwork artwork = Artwork.Draw(100, 100, art =>
        {
            art.Clip(Square, FillRule.EvenOdd);
            art.Fill(Square, TestInks.Red);
        });

        PathOperation clip = Assert.IsType<PathOperation>(Draw(artwork)[0]);

        Assert.Equal((PathPainting.Clip, FillRule.EvenOdd), (clip.Painting, clip.Rule));
    }

    [Fact]
    public void AGradientIsLaidAcrossThePathsBounds()
    {
        VectorPath path = new VectorPath().AddRectangle(10, 20, 30, 40);
        Gradient gradient = Gradient.Across(TestInks.Red, TestInks.Black);
        Artwork artwork = Artwork.Draw(100, 100, art =>
        {
            art.Fill(path, gradient);
            art.Stroke(path, gradient, new LineStyle(1));
        });

        List<DrawOperation> operations = Draw(artwork);

        Assert.Equal(new Bounds(10, 20, 40, 60), Assert.IsType<GradientOperation>(operations[0]).Bounds);
        Assert.Equal(PathPainting.Fill, Assert.IsType<PathOperation>(operations[1]).Painting);
        Assert.IsType<GradientEndOperation>(operations[2]);
        Assert.IsType<GradientOperation>(operations[3]);
        Assert.Equal(PathPainting.Stroke, Assert.IsType<PathOperation>(operations[4]).Painting);
        Assert.IsType<GradientEndOperation>(operations[5]);
    }

    [Fact]
    public void AnImageIsStretchedToItsBox()
    {
        Artwork artwork = Artwork.Draw(100, 100, art => art.Image(new FakeImage(4, 2), 10, 20, 30, 40));

        Assert.Equal(new Bounds(10, 20, 40, 60), Assert.IsType<ImageOperation>(Draw(artwork).Single()).Bounds);
        Assert.Throws<ArgumentNullException>(() => Artwork.Draw(10, 10, art => art.Image(null!, 0, 0, 1, 1)));
    }

    [Theory]
    [InlineData(TextAnchor.Start, 50f)]
    [InlineData(TextAnchor.Middle, 45f)]
    [InlineData(TextAnchor.End, 40f)]
    public void TextIsAnchoredAtItsPoint(TextAnchor anchor, float x)
    {
        // Ten characters at 2 points: 10 points wide to the measurer used in tests.
        Artwork artwork = Artwork.Draw(100, 100, art => art.Text("abcdefghij", 50, 20, TypeStyle.Default.WithPointSize(2), anchor));

        Assert.Equal(x, Assert.IsType<TextOperation>(Draw(artwork).Single()).Position.X, 3);
    }

    [Fact]
    public void TextReachingBeyondAPdfIsLeftOut()
    {
        // Thirty characters at 10^14 points: one and a half times 10^15 points wide, to the measurer used in tests.
        string text = new string('a', 30);
        TypeStyle huge = TypeStyle.Default.WithPointSize(1e14f);
        Artwork artwork = Artwork.Draw(100, 100, art =>
        {
            art.Text(text, 50, 20, huge, TextAnchor.Middle);
            art.Text(text, 50, 20, huge);
            art.Text(text, -50, 20, huge, TextAnchor.End);
        });

        Assert.Equal(50 - 7.5e14, Assert.IsType<TextOperation>(Draw(artwork).Single()).Position.X, 1e9);
    }

    [Theory]
    [InlineData(1e-14f, 1e-14f, ImageFitting.FitWidth)]
    [InlineData(1f, 1e-14f, ImageFitting.Stretch)]
    public void ArtworkTooSmallForAPdfToScaleIsLeftOut(float width, float height, ImageFitting fit) =>
        Assert.Empty(Draw(Artwork.Draw(width, height, art => art.Fill(Square, TestInks.Red)), fit: fit));

    [Fact]
    public void ArtworkIsDrawnWithNumbersAPdfCanHold()
    {
        VectorPath across = new VectorPath().MoveTo(float.NaN, 0).LineTo(1, 1);
        VectorPath down = new VectorPath().MoveTo(0, float.PositiveInfinity).LineTo(1, 1);
        Gradient reaching = Gradient.Between(Offset.Zero, new Offset(1e38f, 0), ofBox: true, [new GradientStop(0, TestInks.Red), new GradientStop(1, TestInks.Black)]);
        VectorPath wide = new VectorPath().AddRectangle(0, 0, 100, 10);

        Artwork.Draw(10, 10, art =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Translate(float.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Translate(2e15f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Text("text", 0, 0, TypeStyle.Default.WithPointSize(2e15f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Translate(0, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Scale(float.PositiveInfinity, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Scale(1, float.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Rotate(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Transform(float.NaN, 0, 0, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Transform(1, float.NaN, 0, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Transform(1, 0, float.NaN, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Transform(1, 0, 0, float.NaN, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Transform(1, 0, 0, 1, float.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Transform(1, 0, 0, 1, 0, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Clip(across));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Fill(down, TestInks.Red));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Fill(across, Gradient.Across(TestInks.Red, TestInks.Black)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Fill(wide, reaching));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Stroke(down, TestInks.Red, new LineStyle(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Stroke(wide, reaching, new LineStyle(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Stroke(wide, TestInks.Red, new LineStyle(float.NaN)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Stroke(wide, TestInks.Red, new LineStyle(1, MiterLimit: float.PositiveInfinity)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Stroke(wide, TestInks.Red, new LineStyle(1, DashOffset: float.NaN)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Stroke(wide, TestInks.Red, new LineStyle(1, Dashes: [2, float.NaN])));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Stroke(wide, Gradient.Across(TestInks.Red, TestInks.Black), new LineStyle(float.NaN)));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Text("text", float.NaN, 0, TypeStyle.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Text("text", 0, float.NaN, TypeStyle.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Image(new FakeImage(1, 1), float.NaN, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Image(new FakeImage(1, 1), 0, float.NaN, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Image(new FakeImage(1, 1), 0, 0, float.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => art.Image(new FakeImage(1, 1), 0, 0, 1, float.NaN));

            // Dashes are optional, and finite ones are fine.
            art.Stroke(wide, TestInks.Red, new LineStyle(1, Dashes: [2, 1]));
        });
    }

    [Fact]
    public void APathIsDrawnAsItWasWhenGiven()
    {
        VectorPath path = new VectorPath().AddRectangle(0, 0, 10, 10);
        List<float> dashes = [2, 1];
        Artwork artwork = Artwork.Draw(100, 100, art =>
        {
            art.Clip(path);
            art.Fill(path, TestInks.Red);
            art.Fill(path, Gradient.Across(TestInks.Red, TestInks.Black));
            art.Stroke(path, TestInks.Black, new LineStyle(1, Dashes: dashes));
            art.Stroke(path, Gradient.Across(TestInks.Red, TestInks.Black), new LineStyle(1));
        });

        // Changed after the calls: a circle added, and a point no PDF can hold.
        path.AddCircle(50, 50, 20).LineTo(float.NaN, 0);
        dashes.Add(float.NaN);

        List<PathOperation> paths = Draw(artwork, new Extent(100, 100)).OfType<PathOperation>().ToList();

        Assert.Equal(5, paths.Count);
        Assert.All(paths, operation => Assert.Equal(new Bounds(0, 0, 10, 10), operation.Bounds));
        Assert.Equal([2f, 1f], paths[3].Style!.Value.Dashes!);
    }

    [Fact]
    public void ReadingSvgFromAStreamLeavesTheStreamOpen()
    {
        using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'/>"));

        Assert.Equal(new Extent(30, 15), Artwork.FromSvg(stream).Size);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void SvgFromAStreamIsReadInTheEncodingItDeclares()
    {
        byte[] latin = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(
            "<?xml version='1.0' encoding='ISO-8859-1'?><svg xmlns='http://www.w3.org/2000/svg' width='100' height='20'><text y='10'>Café</text></svg>");
        using MemoryStream stream = new MemoryStream(latin);
        Artwork artwork = Artwork.FromSvg(stream);

        Assert.Equal("Café", LayoutHarness.Draw(frame => frame.Artwork(artwork), artwork.Size).Operations.OfType<TextOperation>().Single().Text);
    }

    [Theory]
    [InlineData("<?xml version='1.0' encoding='x-no-such-encoding'?><svg xmlns='http://www.w3.org/2000/svg'/>")]
    [InlineData("<?xml version='1.0' encoding='UTF-16'?><svg xmlns='http://www.w3.org/2000/svg'/>")]
    public void SvgFromAStreamInAnEncodingThatCannotBeReadIsNotAnSvg(string svg)
    {
        using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));

        Assert.Throws<FormatException>(() => Artwork.FromSvg(stream));
    }

    [Fact]
    public void ARestoreNeedsASave() =>
        Assert.Throws<InvalidOperationException>(() => Artwork.Draw(10, 10, art => art.RestoreState()));

    [Fact]
    public void StateLeftSavedIsRestoredAtTheEnd()
    {
        Artwork artwork = Artwork.Draw(10, 10, art =>
        {
            art.SaveState();
            art.Translate(5, 5);
        });

        // The harness checks the surface is left as it was found.
        Assert.Empty(Draw(artwork));
    }

    [Theory]
    [InlineData(0f, 10f)]
    [InlineData(10f, 0f)]
    [InlineData(-1f, 10f)]
    [InlineData(float.NaN, 10f)]
    [InlineData(float.PositiveInfinity, 10f)]
    [InlineData(10f, float.PositiveInfinity)]
    public void ArtworkHasASize(float width, float height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Artwork.Draw(width, height, art => { }));

    [Fact]
    public void ArtworkKeepsItsOwnSize() =>
        Assert.Equal(new Extent(40, 30), Artwork.Draw(40, 30, art => { }).Size);

    [Fact]
    public void NothingIsDrawnWithoutWhatItNeeds()
    {
        Assert.Throws<ArgumentNullException>(() => Artwork.Draw(10, 10, null!));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Artwork((Artwork)null!)));
        Artwork.Draw(10, 10, art =>
        {
            Assert.Throws<ArgumentNullException>(() => art.Fill(null!, TestInks.Red));
            Assert.Throws<ArgumentNullException>(() => art.Fill(null!, Gradient.Across(TestInks.Red, TestInks.Black)));
            Assert.Throws<ArgumentNullException>(() => art.Fill(Square, (Gradient)null!));
            Assert.Throws<ArgumentNullException>(() => art.Stroke(null!, TestInks.Red, new LineStyle(1)));
            Assert.Throws<ArgumentNullException>(() => art.Stroke(null!, Gradient.Across(TestInks.Red, TestInks.Black), new LineStyle(1)));
            Assert.Throws<ArgumentNullException>(() => art.Stroke(Square, (Gradient)null!, new LineStyle(1)));
            Assert.Throws<ArgumentNullException>(() => art.Clip(null!));
            Assert.Throws<ArgumentNullException>(() => art.Text(null!, 0, 0, TypeStyle.Default));
            Assert.Throws<ArgumentNullException>(() => art.Text("text", 0, 0, null!));
        });
    }
}
