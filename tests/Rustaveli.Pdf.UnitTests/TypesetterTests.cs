namespace Rustaveli.Pdf.UnitTests;

public class TypesetterTests
{
    private static Document Build(Action<Section> configure)
    {
        return Document.Compose(container =>
        {
            container.Section(configure);
        });
    }

    [Fact]
    public void ProducesOnePageForContentThatFits()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(50f, 50f);
            });
        });
        Assert.Single(LayoutHarness.Render(document).Pages);
    }

    [Fact]
    public void AddsPagesUntilTheContentIsExhausted()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Margins = Sides.All(10f);
            page.Body().Compose(container =>
            {
                container.Slot().Child = new SplittableBlock(10, 50f);
            });
        });
        Assert.Equal(4, LayoutHarness.Render(document).Pages.Count);
    }

    [Fact]
    public void AppliesMarginsToContentPosition()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Margins = new Sides(15f, 25f, 0f, 0f);
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 10f);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = surface.Page(1).Operations.OfType<RectangleOperation>().Last();
        Approximately.Equal(new Offset(15f, 25f), rectangleOperation.Position);
    }

    [Fact]
    public void RepeatsTheHeaderOnEveryPage()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.RunningHead().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(50f, 20f, TestInks.Red);
            });
            page.Body().Compose(container =>
            {
                container.Slot().Child = new SplittableBlock(6, 60f);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        Assert.True(surface.Pages.Count > 1, "The content should span several pages.");
        foreach (RecordedPage page in surface.Pages)
        {
            Assert.Contains(page.Operations.OfType<RectangleOperation>(), (RectangleOperation operation) => operation.Ink == TestInks.Red);
        }
    }

    [Fact]
    public void PlacesTheFooterAgainstTheBottomMargin()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Margins = Sides.All(10f);
            page.RunningFoot().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(50f, 20f, TestInks.Green);
            });
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 10f);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = surface.Page(1).Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Green);
        Approximately.Equal(170f, rectangleOperation.Position.Y);
    }

    [Fact]
    public void ContentIsOffsetBelowTheHeader()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.RunningHead().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(50f, 30f, TestInks.Red);
            });
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 10f, TestInks.Blue);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = surface.Page(1).Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Blue);
        Approximately.Equal(30f, rectangleOperation.Position.Y);
    }

    [Fact]
    public void ResolvesTheTotalPageCountOnTheSecondPass()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.RunningFoot().Text(text =>
            {
                text.Run("Page ");
                text.Folio();
                text.Run(" of ");
                text.PageCount();
            });
            page.Body().Compose(container =>
            {
                container.Slot().Child = new SplittableBlock(4, 100f);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        int count = surface.Pages.Count;
        Assert.Equal($"Page 1 of {count}", surface.Page(1).Content);
        Assert.Equal($"Page {count} of {count}", surface.Page(count).Content);
    }

    [Fact]
    public void UnderlayCoversTheWholeSheetIgnoringMargins()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 300f);
            page.Margins = Sides.All(20f);
            page.Underlay().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(200f, 300f, TestInks.Amber);
            });
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 10f);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = surface.Page(1).Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Amber);
        Approximately.Equal(Offset.Zero, rectangleOperation.Position);
        Approximately.Equal(new Extent(200f, 300f), rectangleOperation.Size);
    }

    [Fact]
    public void OverlayIsDrawnAfterTheBody()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Overlay().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(200f, 200f, TestInks.Cyan);
            });
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 10f, TestInks.Blue);
            });
        });
        List<RectangleOperation> list = LayoutHarness.Render(document).Page(1).Operations.OfType<RectangleOperation>().ToList();
        int content = list.FindIndex(operation => operation.Ink == TestInks.Blue);
        int foreground = list.FindIndex(operation => operation.Ink == TestInks.Cyan);

        Assert.True(foreground > content, "The foreground layer must be painted over the content.");
    }

    [Fact]
    public void NewPageStartsANewPage()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Body().Stack(column =>
            {
                column.Add().Compose(container =>
                {
                    container.Slot().Child = new FixedBlock(10f, 10f);
                });
                column.Add().NewPage();
                column.Add().Compose(container =>
                {
                    container.Slot().Child = new FixedBlock(10f, 10f);
                });
            });
        });
        Assert.Equal(2, LayoutHarness.Render(document).Pages.Count);
    }

    [Fact]
    public void ThrowsWhenContentCanNeverFit()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.RunningHead().Compose(container => container.Slot().Child = new FixedBlock(10f, 50f));
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 500f);
            });
        });
        OversetException ex = Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        // The space quoted is what an empty page has left once the header is placed, not the whole sheet; the trace
        // follows the refusal down from the body to the frame that could not fit.
        Assert.Equal(
            "The body cannot be set even on an empty page, so no further page would help. " +
            "Space available: (Width: 200.000, Height: 150.000). Reason: The block requires " +
            "(Width: 10.000, Height: 500.000) but only (Width: 200.000, Height: 150.000) is available." +
            "\nWhere it did not fit, from the page down:" +
            "\n  Fixed, offered 200 × 150: does not fit — The block requires (Width: 10.000, Height: 500.000) but only " +
            "(Width: 200.000, Height: 150.000) is available.",
            ex.Message);
    }

    [Fact]
    public void ThrowsWhenMarginsLeaveNoRoom()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(100f, 100f);
            page.Margins = Sides.All(60f);
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 10f);
            });
        });
        OversetException ex = Assert.Throws<OversetException>(() => LayoutHarness.Render(document));
        Assert.Equal("The horizontal margins (120.0) leave no room on a page 100.0 points wide.", ex.Message);
    }

    [Fact]
    public void ThrowsWhenTheHeaderAndFooterFillThePage()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 100f);
            page.RunningHead().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 60f);
            });
            page.RunningFoot().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 60f);
            });
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(10f, 10f);
            });
        });
        OversetException ex = Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        // The footer is offered only what the header left over.
        Assert.StartsWith(
            "The running foot does not fit in (Width: 200.000, Height: 40.000). Reason: The block requires " +
            "(Width: 10.000, Height: 60.000) but only (Width: 200.000, Height: 40.000) is available." +
            "\nWhere it did not fit, from the page down:\n  Fixed, offered 200 × 40: does not fit",
            ex.Message);
    }

    [Fact]
    public void ContinuousPagesShrinkToTheirContent()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 800f);
            page.Continuous = true;
            page.Body().Compose(container =>
            {
                container.Slot().Child = new FixedBlock(100f, 60f);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        Approximately.Equal(60f, surface.Page(1).Size.Height);
        Approximately.Equal(200f, surface.Page(1).Size.Width);
    }

    [Fact]
    public void RendersEachPageRunInOrder()
    {
        Document document = Document.Compose(container =>
        {
            container.Section(page =>
            {
                page.Trim = new Extent(200f, 200f);
                page.Body().Compose(inner =>
                {
                    inner.Slot().Child = new FixedBlock(10f, 10f, TestInks.Red);
                });
            });
            container.Section(page =>
            {
                page.Trim = new Extent(300f, 300f);
                page.Body().Compose(inner =>
                {
                    inner.Slot().Child = new FixedBlock(10f, 10f, TestInks.Blue);
                });
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        Assert.Equal(2, surface.Pages.Count);
        Approximately.Equal(new Extent(200f, 200f), surface.Page(1).Size);
        Approximately.Equal(new Extent(300f, 300f), surface.Page(2).Size);
    }

    [Fact]
    public void RegistersSectionsSoLinksCanResolveThem()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Body().Stack(column =>
            {
                column.Add().Compose(container =>
                {
                    container.Slot().Child = new SplittableBlock(3, 150f);
                });
                column.Add().Anchor("end").Compose(container =>
                {
                    container.Slot().Child = new FixedBlock(10f, 10f);
                });
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        DestinationOperation? destinationOperation = surface.Pages.SelectMany(page => page.Operations.OfType<DestinationOperation>()).SingleOrDefault();
        Assert.NotNull(destinationOperation);
        Assert.Equal("end", destinationOperation.Name);
    }

    [Fact]
    public void PrintedTotalMatchesTheActualPageCount()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(70f, 100f);
            page.RunningFoot().Text(text =>
            {
                text.Run("Page ");
                text.Folio();
                text.Run(" of ");
                text.PageCount();
            });
            page.Body().Compose(inner =>
            {
                inner.Slot().Child = new SplittableBlock(96, 10f);
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        int count = surface.Pages.Count;
        foreach (RecordedPage page in surface.Pages)
        {
            Assert.Contains($"of{count}", page.Content.Replace(" ", string.Empty));
        }
    }

    [Fact]
    public void RejectsAPageWithNoArea()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(0f, 0f);
            page.Body().Text("nowhere to draw");
        });
        Assert.Throws<OversetException>(() => LayoutHarness.Render(document));
    }

    [Fact]
    public void ResolvesPageNumbersOfSectionsDeclaredLaterInTheDocument()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200f, 200f);
            page.Body().Stack(column =>
            {
                column.Add().Text(text =>
                {
                    text.Run("Summary on page ");
                    text.FolioOf("summary");
                });
                column.Add().Compose(inner =>
                {
                    inner.Slot().Child = new SplittableBlock(4, 150f);
                });
                column.Add().Anchor("summary").Compose(inner =>
                {
                    inner.Slot().Child = new FixedBlock(10f, 10f);
                });
            });
        });
        RecordingSurface surface = LayoutHarness.Render(document);
        Assert.DoesNotContain("?", surface.Page(1).Content);
    }

    /// <summary>
    /// Page one is an introduction; a chapter anchored by the frame that holds it flows across pages two to four;
    /// every page's running foot says where it stands within the chapter.
    /// </summary>
    private static RecordingSurface ChapterAcrossThreePages() => LayoutHarness.Render(Build(section =>
    {
        section.Trim = new Extent(200f, 200f);
        section.RunningFoot().Text(text =>
        {
            text.Folio(Numerals.LowerRoman);
            text.Run(" | ");
            text.FolioOf("chapter");
            text.Run("-");
            text.LastFolioOf("chapter");
            text.Run(" | ");
            text.FolioWithin("chapter");
            text.Run("/");
            text.PageCountOf("chapter", Numerals.UpperRoman);
        });
        section.Body().Stack(stack =>
        {
            stack.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10f, 150f));
            stack.Add().Anchor("chapter").Compose(inner => inner.Slot().Child = new SplittableBlock(3, 150f));
        });
    }));

    [Fact]
    public void AnAnchorFlowingAcrossPagesBeginsOnItsFirstPage()
    {
        // Content anchored across pages is drawn once per page; the anchor must keep the first of them.
        RecordingSurface surface = ChapterAcrossThreePages();

        Assert.Equal(4, surface.Pages.Count);
        Assert.StartsWith("i | 2-4 |", surface.Page(1).Content);
    }

    [Fact]
    public void RunningFeetKnowWhereTheyStandWithinAnAnchoredChapter()
    {
        RecordingSurface surface = ChapterAcrossThreePages();

        // Before the chapter a page has no place within it, so its number there is counted from the chapter's start.
        Assert.Equal("i | 2-4 | 0/III", surface.Page(1).Content);
        Assert.Equal("ii | 2-4 | 1/III", surface.Page(2).Content);
        Assert.Equal("iii | 2-4 | 2/III", surface.Page(3).Content);
        Assert.Equal("iv | 2-4 | 3/III", surface.Page(4).Content);
    }

    [Fact]
    public void AnchoredNumbersAreUnknownUntilTheAnchorIsReached()
    {
        RecordingSurface surface = LayoutHarness.Render(Build(section =>
        {
            section.Trim = new Extent(200f, 200f);
            section.Body().Text(text =>
            {
                text.LastFolioOf("nowhere");
                text.FolioWithin("nowhere");
                text.PageCountOf("nowhere");
            });
        }));

        Assert.Equal("???", surface.Page(1).Content);
    }

    [Fact]
    public void AnchoredNumbersRefuseAMissingAnchorOrFormat()
    {
        // Composing wraps what the composing code threw, so the argument error is the cause.
        static Exception Cause(Action<TextComposer> compose) =>
            Assert.Throws<CompositionException>(() => Build(section => section.Body().Text(compose))).InnerException!;

        Assert.IsType<ArgumentException>(Cause(text => text.FolioWithin(" ")));
        Assert.IsType<ArgumentNullException>(Cause(text => text.LastFolioOf("a", null!)));
        Assert.IsType<ArgumentNullException>(Cause(text => text.Folio(null!)));
        Assert.IsType<ArgumentNullException>(Cause(text => text.PageCount(null!)));
    }

    [Fact]
    public void WrapsComposeFailuresWithContext()
    {
        CompositionException ex = Assert.Throws<CompositionException>(() => Document.Compose(delegate
        {
            throw new InvalidOperationException("boom");
        }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void RejectsMissingArgumentsByName()
    {
        Document document = Build(page => page.Body().Text("x"));
        RecordingSurface canvas = new RecordingSurface();
        ITypeMeasurer measurer = LayoutHarness.Measurer;

        Assert.Equal(
            "document",
            Assert.Throws<ArgumentNullException>(() => Typesetter.Render(null!, canvas, measurer)).ParamName);
        Assert.Equal(
            "pages",
            Assert.Throws<ArgumentNullException>(() => Typesetter.Render(document, null!, measurer)).ParamName);
        Assert.Equal(
            "measurer",
            Assert.Throws<ArgumentNullException>(() => Typesetter.Render(document, canvas, null!)).ParamName);
    }

    [Theory]
    [InlineData(0f, 100f, "(Width: 0.000, Height: 100.000)")]
    [InlineData(100f, 0f, "(Width: 100.000, Height: 0.000)")]
    [InlineData(-10f, 100f, "(Width: -10.000, Height: 100.000)")]
    public void RejectsAPageSizeWithoutAreaInEitherDimension(float width, float height, string formattedSize)
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(width, height);
            page.Body().Text("nowhere to draw");
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            $"The trim size {formattedSize} cannot be drawn. Both dimensions must be greater than zero.",
            exception.Message);
    }

    [Fact]
    public void RejectsHorizontalMarginsThatConsumeExactlyTheWholeWidth()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(100, 100);
            page.Margins = Sides.Symmetric(horizontal: 50, vertical: 0);
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal("The horizontal margins (100.0) leave no room on a page 100.0 points wide.", exception.Message);
    }

    [Fact]
    public void RejectsVerticalMarginsThatConsumeExactlyTheWholeHeight()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(100, 100);
            page.Margins = Sides.Symmetric(horizontal: 0, vertical: 50);
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal("The vertical margins (100.0) leave no room on a page 100.0 points tall.", exception.Message);
    }

    [Fact]
    public void ContinuousPagesMeasureVerticalMarginsAgainstTheMaximumHeightRatherThanTheNominalOne()
    {
        // Margins taller than the nominal page would be rejected on a fixed page; a continuous page grows past it.
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Margins = Sides.Symmetric(horizontal: 0, vertical: 60);
            page.Continuous = true;
            page.Body().Compose(container => container.Slot().Child = new FixedBlock(10, 10));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(new Extent(200, 130), page.Size);
    }

    [Fact]
    public void ContinuousPagesGrowToHoldMarginsBandsAndContentTallerThanTheNominalPage()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Margins = new Sides(Left: 0, Top: 10, Right: 0, Bottom: 20);
            page.Continuous = true;
            page.RunningHead().Compose(container => container.Slot().Child = new FixedBlock(50, 15, TestInks.Red));
            page.Body().Compose(container => container.Slot().Child = new FixedBlock(50, 500, TestInks.Blue));
            page.RunningFoot().Compose(container => container.Slot().Child = new FixedBlock(50, 25, TestInks.Green));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(new Extent(200, 570), page.Size);
        Approximately.Equal(10f, Rectangle(page, TestInks.Red).Position.Y);
        Approximately.Equal(25f, Rectangle(page, TestInks.Blue).Position.Y);
        Approximately.Equal(525f, Rectangle(page, TestInks.Green).Position.Y);
    }

    [Fact]
    public void RejectsAHeaderThatDoesNotFitOnThePage()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 100);
            page.RunningHead().Compose(container => container.Slot().Child = new FixedBlock(10, 150));
            page.Body().Compose(container => container.Slot().Child = new FixedBlock(10, 10));
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.StartsWith(
            "The running head does not fit in (Width: 200.000, Height: 100.000). Reason: The block requires " +
            "(Width: 10.000, Height: 150.000) but only (Width: 200.000, Height: 100.000) is available." +
            "\nWhere it did not fit, from the page down:\n  Fixed, offered 200 × 100: does not fit",
            exception.Message);
    }

    [Fact]
    public void RejectsAHeaderThatClaimsMoreThanItWasOffered()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 100);
            page.RunningHead().Compose(container => container.Slot().Child = new OversizedBlock(10, 300));
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal("The running head (300.0 points) is taller than the page.", exception.Message);
    }

    [Fact]
    public void RejectsAHeaderThatExpandsToFillThePage()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Margins = Sides.Symmetric(horizontal: 0, vertical: 10);
            page.RunningHead().ExpandVertically().Text("Title");
            page.Body().Text("Body");
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "The running head took all 80.0 points available, leaving no room for the body or the running foot. " +
            "This usually means it holds content that expands to fill the space offered to it, such as " +
            "Expand. Give the running head an explicit Height, or remove the expanding content.",
            exception.Message);
    }

    [Fact]
    public void RejectsAFooterThatClaimsMoreThanTheHeaderLeft()
    {
        using CultureScope culture = CultureScope.DecimalComma();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 200);
            page.RunningHead().Compose(container => container.Slot().Child = new FixedBlock(10, 50));
            page.RunningFoot().Compose(container => container.Slot().Child = new OversizedBlock(10, 200));
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "The running head (50.0) and running foot (200.0) together exceed the 200.0 points available for the body.",
            exception.Message);
    }

    [Fact]
    public void AcceptsBandsThatOvershootThePageOnlyWithinTheLayoutTolerance()
    {
        // Every element treats an overshoot below Size.Epsilon as fitting, so a footer may legitimately report a
        // fraction of a thousandth more than it was offered. The page must accept what its own footer accepted.
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 200);
            page.RunningHead().Compose(container => container.Slot().Child = new FixedBlock(10, 50, TestInks.Red));
            page.RunningFoot().Compose(container => container.Slot().Child = new FixedBlock(10, 150.0005f, TestInks.Green));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(50f, Rectangle(page, TestInks.Green).Position.Y);
    }

    [Fact]
    public void WrapsADrawingFailureWithThePageItHappenedOn()
    {
        InvalidOperationException failure = new InvalidOperationException("The image could not be decoded.");
        Document document = Document.Compose(container =>
        {
            container.Section(page => page.Body().Compose(inner => inner.Slot().Child = new FixedBlock(10, 10)));
            container.Section(page => page.Body().Compose(inner => inner.Slot().Child = new ThrowingBlock(failure)));
        });

        RenderingException exception =
            Assert.Throws<RenderingException>(() => LayoutHarness.Render(document));

        Assert.Equal("Drawing page 2 failed. See the inner exception for details.", exception.Message);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public void WrapsAFailureWhileMeasuringWithThePageItHappenedOn()
    {
        InvalidOperationException failure = new InvalidOperationException("The component could not measure itself.");
        Document document = Document.Compose(container =>
        {
            container.Section(page => page.Body().Compose(inner => inner.Slot().Child = new FixedBlock(10, 10)));
            container.Section(page => page.Body().Compose(inner => inner.Slot().Child = new ThrowingBlock(failure, whileMeasured: true)));
        });

        RenderingException exception = Assert.Throws<RenderingException>(() => LayoutHarness.Render(document));

        Assert.Equal("Laying out page 2 failed. See the inner exception for details.", exception.Message);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public void WrapsAFailureWhileMeasuringARunningHead()
    {
        InvalidOperationException failure = new InvalidOperationException("The component could not measure itself.");
        Document document = Build(page => page.RunningHead().Compose(inner => inner.Slot().Child = new ThrowingBlock(failure, whileMeasured: true)));

        Assert.Same(failure, Assert.Throws<RenderingException>(() => LayoutHarness.Render(document)).InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LetsALayoutFailureRaisedWhileMeasuringPassThroughUnwrapped(bool rendering)
    {
        TypesettingException failure = rendering ? new RenderingException("A nested document failed.") : new OversetException("A nested layout could not be resolved.");
        Document document = Build(page => page.Body().Compose(inner => inner.Slot().Child = new ThrowingBlock(failure, whileMeasured: true)));

        Assert.Same(failure, Assert.Throws(failure.GetType(), () => LayoutHarness.Render(document)));
    }

    [Fact]
    public void LetsALayoutFailureRaisedWhileDrawingPassThroughUnwrapped()
    {
        OversetException failure = new OversetException("A nested layout could not be resolved.");
        Document document = Build(page => page.Body().Compose(inner => inner.Slot().Child = new ThrowingBlock(failure)));

        Assert.Same(failure, Assert.Throws<OversetException>(() => LayoutHarness.Render(document)));
    }

    [Fact]
    public void DoesNotWrapADrawingFailureASecondTime()
    {
        RenderingException failure = new RenderingException("A nested document failed to draw.");
        Document document = Build(page => page.Body().Compose(inner => inner.Slot().Child = new ThrowingBlock(failure)));

        Assert.Same(failure, Assert.Throws<RenderingException>(() => LayoutHarness.Render(document)));
    }

    [Fact]
    public void GivesUpOnAPageRunThatNeverStopsAskingForAnotherPage()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(100, 100);
            page.Body().Compose(container => container.Slot().Child = new NeverFinishingBlock());
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "The document exceeded 10000 pages, which usually means some content reports more to come but never takes " +
            "any space. A document that really is longer can raise its PageLimit.",
            exception.Message);
    }

    [Fact]
    public void AcceptsAPageRunOfExactlyTheMaximumLength()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(20, 10);
            page.Body().Compose(container => container.Slot().Child = new SplittableBlock(10_000, 10f));
        });

        Assert.Equal(10_000, LayoutHarness.Render(document).Pages.Count);
    }

    [Fact]
    public void RejectsAPageRunOnePageLongerThanTheMaximum()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(20, 10);
            page.Body().Compose(container => container.Slot().Child = new SplittableBlock(10_001, 10f));
        });

        OversetException exception =
            Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.StartsWith("The document exceeded 10000 pages", exception.Message);
    }

    [Fact]
    public void ThePageLimitCountsEverySectionAndCanBeSet()
    {
        Document document = Document.Compose(composition =>
        {
            composition.Section(page =>
            {
                page.Trim = new Extent(20, 10);
                page.Body().Compose(container => container.Slot().Child = new SplittableBlock(3, 10f));
            });
            composition.Section(page =>
            {
                page.Trim = new Extent(20, 10);
                page.Body().Compose(container => container.Slot().Child = new SplittableBlock(2, 10f));
            });
        });

        document.PageLimit = 5;
        Assert.Equal(5, LayoutHarness.Render(document).Pages.Count);

        document.PageLimit = 4;
        OversetException exception = Assert.Throws<OversetException>(() => LayoutHarness.Render(document));
        Assert.StartsWith("The document exceeded 4 pages", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APageLimitAllowsAPageAtLeast(int limit) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(page => page.Body().Text("x")).PageLimit = limit);

    [Fact]
    public void ThePageLimitIsTenThousandUnlessSet() => Assert.Equal(10_000, Build(page => page.Body().Text("x")).PageLimit);

    [Fact]
    public void CountsPagesQuotingEachAsTheLastThenDrawsWithTheSettledTotal()
    {
        PaginationRecorder recorder = new PaginationRecorder();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Underlay().Compose(container => container.Slot().Child = recorder);
            page.Body().Compose(container => container.Slot().Child = new SplittableBlock(3, 100f));
        });

        LayoutHarness.Render(document);

        Assert.Equal(
            new[]
            {
                // First counting pass: the total is not known yet, so each page quotes itself as the last.
                (1, 1, false), (2, 2, false), (3, 3, false),

                // Second counting pass: fed the total of three, it counts three again and the count has settled.
                (1, 3, true), (2, 3, true), (3, 3, true),

                // The drawing pass.
                (1, 3, true), (2, 3, true), (3, 3, true),
            },
            recorder.Draws);
    }

    [Fact]
    public void StopsRecountingAfterFivePassesWhenTheCountNeverSettles()
    {
        OscillatingBlock content = new OscillatingBlock();
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Compose(container => container.Slot().Child = content);
        });

        RecordingSurface canvas = LayoutHarness.Render(document);

        // Five counting passes alternate between one and two pages, ending on one; the drawing pass is then told
        // the document is one page long, which this content answers with two.
        Assert.Equal(6, content.Passes);
        Assert.Equal(2, canvas.Pages.Count);
    }

    [Fact]
    public void RedrawsStatefulBandsInFullOnEveryPage()
    {
        // Each band holds content that reports itself spent once drawn. Only the per-page reset brings it back.
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 200);
            page.Underlay().Compose(container => container.Slot().Child = new SplittableBlock(1, 5f, width: 13));
            page.RunningHead().Compose(container => container.Slot().Child = new SplittableBlock(1, 20f, width: 11));
            page.Body().Compose(container => container.Slot().Child = new SplittableBlock(3, 160f, width: 15));
            page.RunningFoot().Compose(container => container.Slot().Child = new SplittableBlock(1, 20f, width: 12));
            page.Overlay().Compose(container => container.Slot().Child = new SplittableBlock(1, 5f, width: 14));
        });

        RecordingSurface canvas = LayoutHarness.Render(document);

        Assert.Equal(3, canvas.Pages.Count);
        foreach (RecordedPage page in canvas.Pages)
        {
            float[] widthsInDrawOrder = page.Operations
                .OfType<RectangleOperation>()
                .Where(operation => operation.Ink == TestInks.Blue)
                .Select(operation => operation.Size.Width)
                .ToArray();

            Assert.Equal(new[] { 13f, 11f, 15f, 12f, 14f }, widthsInDrawOrder);
        }
    }

    [Fact]
    public void ShowsARunningHeadMarkedOnceOnlyOnTheFirstPageOfTheFinalOutput()
    {
        // The counting passes have already drawn the header once, so the drawing pass must start from a full
        // reset; the per-page reset in between must leave the "already shown" flag alone.
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 200);
            page.RunningHead().Once().Compose(container => container.Slot().Child = new FixedBlock(50, 20, TestInks.Red));
            page.Body().Compose(container => container.Slot().Child = new SplittableBlock(3, 150f));
        });

        RecordingSurface canvas = LayoutHarness.Render(document);

        int[] headersPerPage = canvas.Pages
            .Select(page => page.Operations.OfType<RectangleOperation>().Count(operation => operation.Ink == TestInks.Red))
            .ToArray();
        Assert.Equal(new[] { 1, 0, 0 }, headersPerPage);
    }

    [Fact]
    public void PaintsThePaperAcrossTheWholeSheetIgnoringMargins()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 300);
            page.Margins = Sides.All(20);
            page.Paper = TestInks.Amber;
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);
        RectangleOperation fill = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Amber, fill.Ink);
        Approximately.Equal(Offset.Zero, fill.Position);
        Approximately.Equal(new Extent(200, 300), fill.Size);
    }

    [Fact]
    public void PaintsNothingForTransparentPaper()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 300);
            page.Paper = TestInks.Transparent;
        });

        Assert.Empty(Assert.Single(LayoutHarness.Render(document).Pages).Operations);
    }

    [Fact]
    public void PlacesEveryBandRelativeToTheMarginsAndTheOverlayAtThePageCorner()
    {
        Document document = Build(page =>
        {
            page.Trim = new Extent(200, 300);
            page.Margins = Sides.All(20);
            page.RunningHead().Compose(container => container.Slot().Child = new FixedBlock(50, 30, TestInks.Red));
            page.Body().Compose(container => container.Slot().Child = new FixedBlock(50, 10, TestInks.Blue));
            page.RunningFoot().Compose(container => container.Slot().Child = new FixedBlock(50, 30, TestInks.Green));
            page.Overlay().Compose(container => container.Slot().Child = new FixedBlock(200, 300, TestInks.Cyan));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(new Offset(20, 20), Rectangle(page, TestInks.Red).Position);
        Approximately.Equal(new Offset(20, 50), Rectangle(page, TestInks.Blue).Position);
        Approximately.Equal(new Offset(20, 250), Rectangle(page, TestInks.Green).Position);
        Approximately.Equal(Offset.Zero, Rectangle(page, TestInks.Cyan).Position);
    }

    private static RectangleOperation Rectangle(RecordedPage page, Ink color) =>
        page.Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == color);
}
