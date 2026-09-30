namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Images and artwork generated for the box they fill, when the page is drawn.
/// </summary>
public class GeneratedContentTests
{
    private static readonly byte[] Pixel = Rustaveli.Pdf.Images.PngWriter.Rgb(1, 1, [255, 0, 0]);

    [Fact]
    public void AnImageIsAskedForItsBoxAndThePixelsThatTakes()
    {
        List<ImageRequest> requests = [];
        RecordedPage page = LayoutHarness.Render(frame => frame.Image(request => { requests.Add(request); return Pixel; }), new Extent(100, 50));

        // 288 pixels an inch unless the export says otherwise: four pixels to the point.
        Assert.Equal(new ImageRequest(new Extent(100, 50), 400, 200, 288), Assert.Single(requests));
        Assert.Equal(new Bounds(0, 0, 100, 50), Assert.IsType<ImageOperation>(Assert.Single(page.Operations)).Bounds);
    }

    [Fact]
    public void AGeneratedImageTakesAllTheRoomThereIs() =>
        Assert.Equal(new Extent(80, 30), LayoutHarness.Plan(frame => frame.Image(_ => Pixel), new Extent(80, 30)).Size);

    [Fact]
    public void AnImageIsGeneratedAtTheResolutionInForce()
    {
        ImageRequest? asked = null;
        PlanContext context = LayoutHarness.Context();
        context.Resolution = 72;

        LayoutHarness.Render(LayoutHarness.Build(frame => frame.Image(request => { asked = request; return Pixel; })), new Extent(10.5f, 10), context);

        Assert.Equal((11, 10, 72f), (asked!.Value.PixelWidth, asked.Value.PixelHeight, asked.Value.Resolution));
    }

    [Fact]
    public void NothingGeneratedLeavesTheBoxEmpty()
    {
        Assert.Empty(LayoutHarness.Render(frame => frame.Image(_ => null), new Extent(10, 10)).Operations);
        Assert.Empty(LayoutHarness.Render(frame => frame.Image(_ => []), new Extent(10, 10)).Operations);
        Assert.Empty(LayoutHarness.Render(frame => frame.Artwork(_ => null), new Extent(10, 10)).Operations);
    }

    [Theory]
    [InlineData(0f, 10f)]
    [InlineData(10f, 0f)]
    public void NothingIsGeneratedForABoxOfNoSize(float width, float height)
    {
        int calls = 0;

        LayoutHarness.Render(frame => frame.Image(_ => { calls++; return Pixel; }), new Extent(width, height));
        LayoutHarness.Render(frame => frame.Artwork(_ => { calls++; return null; }), new Extent(width, height));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void ContentIsGeneratedOnlyForThePassThatDraws()
    {
        int images = 0, artworks = 0;
        Document document = Document.Compose(frame => frame.Section(section =>
        {
            section.Trim = new Extent(100, 100);
            section.Body().Stack(stack =>
            {
                stack.Add().Height(40).Image(_ => { images++; return Pixel; });
                stack.Add().Height(40).Artwork(_ => { artworks++; return null; });
                stack.Add().Text(text => text.PageCount());
            });
        }));

        LayoutHarness.Render(document);

        Assert.Equal((1, 1), (images, artworks));
    }

    [Fact]
    public void ArtworkIsGeneratedForItsBoxAndStretchedToIt()
    {
        Extent? asked = null;
        RecordedPage page = LayoutHarness.Render(
            frame => frame.Artwork(size =>
            {
                asked = size;
                return Artwork.Draw(10, 10, art => art.Fill(new VectorPath().AddRectangle(0, 0, 10, 10), TestInks.Red));
            }),
            new Extent(60, 30));

        Assert.Equal(new Extent(60, 30), asked);
        Assert.Equal(new Bounds(0, 0, 60, 30), page.Operations.OfType<PathOperation>().Single().Bounds);
    }

    [Fact]
    public void AGeneratorIsNeeded()
    {
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Image((Func<ImageRequest, byte[]?>)null!)));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Artwork((Func<Extent, Artwork?>)null!)));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-72f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void AResolutionIsAboveNothing(float resolution) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfExportOptions { ImageResolution = resolution });

    [Fact]
    public void TheExportsResolutionIsTwoHundredAndEightyEightUnlessSet()
    {
        Assert.Equal(288f, new PdfExportOptions().ImageResolution);
        Assert.Equal(150f, new PdfExportOptions { ImageResolution = 150 }.ImageResolution);
    }
}
