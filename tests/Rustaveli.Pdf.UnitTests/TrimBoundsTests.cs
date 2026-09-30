namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Pages sized by their content between a smallest and a largest trim.
/// </summary>
public class TrimBoundsTests
{
    private static List<RecordedPage> Render(Action<Section> configure) =>
        LayoutHarness.Render(Document.Compose(frame => frame.Section(configure))).Pages;

    [Fact]
    public void APageTakesItsContentsSizeWithinTheBounds()
    {
        RecordedPage page = Assert.Single(Render(section =>
        {
            section.MinimumTrim = new Extent(50, 50);
            section.MaximumTrim = new Extent(400, 400);
            section.Margins = Sides.All(10);
            section.Body().Compose(frame => frame.Slot().Child = new FixedBlock(120, 80));
        }));

        Approximately.Equal(new Extent(140, 100), page.Size);
    }

    [Fact]
    public void APageIsNoSmallerThanTheSmallestTrim()
    {
        RecordedPage page = Assert.Single(Render(section =>
        {
            section.MinimumTrim = new Extent(200, 150);
            section.MaximumTrim = new Extent(400, 400);
            section.Body().Compose(frame => frame.Slot().Child = new FixedBlock(20, 10));
        }));

        Approximately.Equal(new Extent(200, 150), page.Size);
    }

    [Fact]
    public void ContentBeyondTheLargestTrimFlowsOnToPagesOfTheLargestSize()
    {
        List<RecordedPage> pages = Render(section =>
        {
            section.MinimumTrim = new Extent(10, 10);
            section.MaximumTrim = new Extent(100, 90);
            section.Body().Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 30));
        });

        // Three units fill the largest page; the last one sits on a page of its own size, as narrow as the unit.
        Assert.Equal(2, pages.Count);
        Approximately.Equal(new Extent(10, 90), pages[0].Size);
        Approximately.Equal(new Extent(10, 30), pages[1].Size);
    }

    [Fact]
    public void OnlyAMinimumRangesUpToTheTrim()
    {
        RecordedPage page = Assert.Single(Render(section =>
        {
            section.Trim = new Extent(300, 300);
            section.MinimumTrim = new Extent(20, 20);
            section.Body().ExpandHorizontally().Compose(frame => frame.Slot().Child = new FixedBlock(20, 50));
        }));

        Approximately.Equal(300f, page.Size.Width);
    }

    [Fact]
    public void OnlyAMaximumRangesDownToTheTrim()
    {
        RecordedPage page = Assert.Single(Render(section =>
        {
            section.Trim = new Extent(100, 100);
            section.MaximumTrim = new Extent(300, 300);
            section.Body().Compose(frame => frame.Slot().Child = new FixedBlock(20, 200));
        }));

        Approximately.Equal(new Extent(100, 200), page.Size);
    }

    [Fact]
    public void ThePageIsAsWideAsItsWidestBand()
    {
        RecordedPage page = Assert.Single(Render(section =>
        {
            section.MinimumTrim = Extent.Zero;
            section.MaximumTrim = new Extent(400, 400);
            section.RunningHead().Compose(frame => frame.Slot().Child = new FixedBlock(150, 10));
            section.RunningFoot().Compose(frame => frame.Slot().Child = new FixedBlock(90, 10));
            section.Body().Compose(frame => frame.Slot().Child = new FixedBlock(60, 30));
        }));

        Approximately.Equal(new Extent(150, 50), page.Size);
    }

    [Fact]
    public void TheBodyFillsWhatTheSmallestTrimLeavesIt()
    {
        RecordedPage page = Assert.Single(Render(section =>
        {
            section.MinimumTrim = new Extent(200, 150);
            section.MaximumTrim = new Extent(400, 400);
            section.Margins = Sides.All(10);
            section.Body().Fill(TestInks.Red).Compose(frame => frame.Slot().Child = new FixedBlock(20, 10));
        }));

        RectangleOperation fill = page.Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Red);
        Approximately.Equal(new Extent(180, 130), fill.Size);
    }

    public static TheoryData<string, Action<Section>> SizesThatCannotBeDrawn => new()
    {
        { "Trim", section => section.Trim = new Extent(0, 100) },
        { "Trim", section => section.Trim = new Extent(100, -1) },
        { "Trim", section => section.Trim = new Extent(float.NaN, 100) },
        { "Trim", section => section.Trim = new Extent(100, float.PositiveInfinity) },
        { "Trim", section => section.Trim = new Extent(20_000, 100) },
        { "MinimumTrim", section => section.MinimumTrim = new Extent(-1, 100) },
        { "MinimumTrim", section => section.MinimumTrim = new Extent(100, float.NaN) },
        { "MinimumTrim", section => section.MinimumTrim = new Extent(100, 14_401) },
        { "MaximumTrim", section => section.MaximumTrim = new Extent(200, 0) },
        { "MaximumTrim", section => section.MaximumTrim = new Extent(float.NaN, 200) },
        { "MaximumTrim", section => section.MaximumTrim = new Extent(20_000, 20_000) },
        { "Margins", section => section.Margins = Sides.All(float.NaN) },
        { "Margins", section => section.Margins = new Sides(0, 0, -1, 0) },
        { "Margins", section => section.Margins = new Sides(0, float.PositiveInfinity, 0, 0) },
    };

    [Theory]
    [MemberData(nameof(SizesThatCannotBeDrawn))]
    public void ASizeThatCannotBeDrawnIsRefusedWhereItIsSet(string property, Action<Section> set)
    {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(() => set(new Section()));

        Assert.Equal(property, refused.ParamName);
    }

    [Fact]
    public void TrimsUpToWhatPdfAllowsAreAccepted()
    {
        Section section = new Section
        {
            Trim = Extent.Max,
            MinimumTrim = Extent.Zero,
            MaximumTrim = Extent.Max,
            Margins = Sides.All(0),
        };

        section.MinimumTrim = null;
        section.MaximumTrim = null;

        Assert.Equal((Extent.Max, Extent.Max), (section.SmallestTrim, section.LargestTrim));
    }

    [Fact]
    public void TheBoundsOfAContinuousPageRunFromNothingToTheTallestPage()
    {
        Section section = new Section { Trim = new Extent(200, 300), Continuous = true };

        Assert.Equal(new Extent(200, 0), section.SmallestTrim);
        Assert.Equal(new Extent(200, Extent.Max.Height), section.LargestTrim);
    }

    [Fact]
    public void AFixedPageIsBoundedByItsTrim()
    {
        Section section = new Section { Trim = new Extent(200, 300) };

        Assert.Equal(section.Trim, section.SmallestTrim);
        Assert.Equal(section.Trim, section.LargestTrim);
    }

    [Fact]
    public void ASmallestTrimLargerThanTheLargestIsRefused()
    {
        OversetException exception = Assert.Throws<OversetException>(() => Render(section =>
        {
            section.MinimumTrim = new Extent(300, 100);
            section.MaximumTrim = new Extent(200, 200);
            section.Body().Compose(frame => frame.Slot().Child = new FixedBlock(20, 10));
        }));

        Assert.Contains("The smallest trim", exception.Message);
    }
}
