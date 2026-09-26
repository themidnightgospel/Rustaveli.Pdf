using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

public class DocumentGeneratorTests
{
    private static Document Build(Action<PageDescriptor> configure)
    {
        return Document.Create(delegate(IDocumentContainer container)
        {
            container.Page(configure);
        });
    }

    [Fact]
    public void ProducesOnePageForContentThatFits()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(50f, 50f);
            });
        });
        Assert.Single(LayoutHarness.Render(document).Pages);
    }

    [Fact]
    public void AddsPagesUntilTheContentIsExhausted()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Margin = Edges.All(10f);
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new SplittableElement(10, 50f);
            });
        });
        Assert.Equal(4, LayoutHarness.Render(document).Pages.Count);
    }

    [Fact]
    public void AppliesMarginsToContentPosition()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Margin = new Edges(15f, 25f, 0f, 0f);
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = recordingCanvas.Page(1).Operations.OfType<RectangleOperation>().Last();
        Approximately.Equal(new Position(15f, 25f), rectangleOperation.Position);
    }

    [Fact]
    public void RepeatsTheHeaderOnEveryPage()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Header().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(50f, 20f, Colors.Red);
            });
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new SplittableElement(6, 60f);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        Assert.True(recordingCanvas.Pages.Count > 1, "The content should span several pages.");
        foreach (RecordedPage page in recordingCanvas.Pages)
        {
            Assert.Contains(page.Operations.OfType<RectangleOperation>(), (RectangleOperation operation) => operation.Color == Colors.Red);
        }
    }

    [Fact]
    public void PlacesTheFooterAgainstTheBottomMargin()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Margin = Edges.All(10f);
            page.Footer().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(50f, 20f, Colors.Green);
            });
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = recordingCanvas.Page(1).Operations.OfType<RectangleOperation>().Single((RectangleOperation operation) => operation.Color == Colors.Green);
        Approximately.Equal(170f, rectangleOperation.Position.Y);
    }

    [Fact]
    public void ContentIsOffsetBelowTheHeader()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Header().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(50f, 30f, Colors.Red);
            });
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f, Colors.Blue);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = recordingCanvas.Page(1).Operations.OfType<RectangleOperation>().Single((RectangleOperation operation) => operation.Color == Colors.Blue);
        Approximately.Equal(30f, rectangleOperation.Position.Y);
    }

    [Fact]
    public void ResolvesTheTotalPageCountOnTheSecondPass()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Footer().Text(delegate(TextDescriptor text)
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new SplittableElement(4, 100f);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        int count = recordingCanvas.Pages.Count;
        Assert.Equal($"Page 1 of {count}", recordingCanvas.Page(1).Content);
        Assert.Equal($"Page {count} of {count}", recordingCanvas.Page(count).Content);
    }

    [Fact]
    public void BackgroundCoversTheWholeSheetIgnoringMargins()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 300f);
            page.Margin = Edges.All(20f);
            page.Background().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(200f, 300f, Colors.Amber);
            });
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        RectangleOperation rectangleOperation = recordingCanvas.Page(1).Operations.OfType<RectangleOperation>().Single((RectangleOperation operation) => operation.Color == Colors.Amber);
        Approximately.Equal(Position.Zero, rectangleOperation.Position);
        Approximately.Equal(new Size(200f, 300f), rectangleOperation.Size);
    }

    [Fact]
    public void ForegroundIsDrawnAfterTheContent()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Foreground().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(200f, 200f, Colors.Cyan);
            });
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f, Colors.Blue);
            });
        });
        List<RectangleOperation> list = LayoutHarness.Render(document).Page(1).Operations.OfType<RectangleOperation>().ToList();
        int content = list.FindIndex(operation => operation.Color == Colors.Blue);
        int foreground = list.FindIndex(operation => operation.Color == Colors.Cyan);

        Assert.True(foreground > content, "The foreground layer must be painted over the content.");
    }

    [Fact]
    public void PageBreakStartsANewPage()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Content().Column(delegate(ColumnDescriptor column)
            {
                column.Item().Element(delegate(IContainer container)
                {
                    container.Child = new FixedElement(10f, 10f);
                });
                column.Item().PageBreak();
                column.Item().Element(delegate(IContainer container)
                {
                    container.Child = new FixedElement(10f, 10f);
                });
            });
        });
        Assert.Equal(2, LayoutHarness.Render(document).Pages.Count);
    }

    [Fact]
    public void ThrowsWhenContentCanNeverFit()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Header().Element(container => container.Child = new FixedElement(10f, 50f));
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 500f);
            });
        });
        DocumentLayoutException ex = Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        // The space quoted is what an empty page has left once the header is placed, not the whole sheet.
        Assert.Equal(
            "The page content cannot be drawn even on an empty page, so no additional page would help. " +
            "Available space: (Width: 200.000, Height: 150.000). Reason: The element requires " +
            "(Width: 10.000, Height: 500.000) but only (Width: 200.000, Height: 150.000) is available.",
            ex.Message);
    }

    [Fact]
    public void ThrowsWhenMarginsLeaveNoRoom()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(100f, 100f);
            page.Margin = Edges.All(60f);
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f);
            });
        });
        DocumentLayoutException ex = Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));
        Assert.Equal("The horizontal margins (120.0) leave no room on a page 100.0 points wide.", ex.Message);
    }

    [Fact]
    public void ThrowsWhenTheHeaderAndFooterFillThePage()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 100f);
            page.Header().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 60f);
            });
            page.Footer().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 60f);
            });
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f);
            });
        });
        DocumentLayoutException ex = Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        // The footer is offered only what the header left over.
        Assert.Equal(
            "The page footer does not fit in (Width: 200.000, Height: 40.000). Reason: The element requires " +
            "(Width: 10.000, Height: 60.000) but only (Width: 200.000, Height: 40.000) is available.",
            ex.Message);
    }

    [Fact]
    public void ContinuousPagesShrinkToTheirContent()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 800f);
            page.IsContinuous = true;
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(100f, 60f);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        Approximately.Equal(60f, recordingCanvas.Page(1).Size.Height);
        Approximately.Equal(200f, recordingCanvas.Page(1).Size.Width);
    }

    [Fact]
    public void RendersEachPageRunInOrder()
    {
        Document document = Document.Create(delegate(IDocumentContainer container)
        {
            container.Page(delegate(PageDescriptor page)
            {
                page.Size = new Size(200f, 200f);
                page.Content().Element(delegate(IContainer inner)
                {
                    inner.Child = new FixedElement(10f, 10f, Colors.Red);
                });
            });
            container.Page(delegate(PageDescriptor page)
            {
                page.Size = new Size(300f, 300f);
                page.Content().Element(delegate(IContainer inner)
                {
                    inner.Child = new FixedElement(10f, 10f, Colors.Blue);
                });
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        Assert.Equal(2, recordingCanvas.Pages.Count);
        Approximately.Equal(new Size(200f, 200f), recordingCanvas.Page(1).Size);
        Approximately.Equal(new Size(300f, 300f), recordingCanvas.Page(2).Size);
    }

    [Fact]
    public void RegistersSectionsSoLinksCanResolveThem()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Content().Column(delegate(ColumnDescriptor column)
            {
                column.Item().Element(delegate(IContainer container)
                {
                    container.Child = new SplittableElement(3, 150f);
                });
                column.Item().Section("end").Element(delegate(IContainer container)
                {
                    container.Child = new FixedElement(10f, 10f);
                });
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        DestinationOperation? destinationOperation = recordingCanvas.Pages.SelectMany((RecordedPage page) => page.Operations.OfType<DestinationOperation>()).SingleOrDefault();
        Assert.NotNull(destinationOperation);
        Assert.Equal("end", destinationOperation.Name);
    }

    [Fact]
    public void PrintedTotalMatchesTheActualPageCount()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(70f, 100f);
            page.Footer().Text(delegate(TextDescriptor text)
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
            page.Content().Element(delegate(IContainer inner)
            {
                inner.Child = new SplittableElement(96, 10f);
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        int count = recordingCanvas.Pages.Count;
        foreach (RecordedPage page in recordingCanvas.Pages)
        {
            Assert.Contains($"of{count}", page.Content.Replace(" ", string.Empty));
        }
    }

    [Fact]
    public void RejectsAPageWithNoArea()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(0f, 0f);
            page.Content().Text("nowhere to draw");
        });
        Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));
    }

    [Fact]
    public void ResolvesPageNumbersOfSectionsDeclaredLaterInTheDocument()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Content().Column(delegate(ColumnDescriptor column)
            {
                column.Item().Text(delegate(TextDescriptor text)
                {
                    text.Span("Summary on page ");
                    text.PageNumberOfSection("summary");
                });
                column.Item().Element(delegate(IContainer inner)
                {
                    inner.Child = new SplittableElement(4, 150f);
                });
                column.Item().Section("summary").Element(delegate(IContainer inner)
                {
                    inner.Child = new FixedElement(10f, 10f);
                });
            });
        });
        RecordingCanvas recordingCanvas = LayoutHarness.Render(document);
        Assert.DoesNotContain("?", recordingCanvas.Page(1).Content);
    }

    [Fact]
    public void WrapsComposeFailuresWithContext()
    {
        DocumentComposeException ex = Assert.Throws<DocumentComposeException>(() => Document.Create(delegate
        {
            throw new InvalidOperationException("boom");
        }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void RejectsMissingArgumentsByName()
    {
        Document document = Build(page => page.Content().Text("x"));
        RecordingCanvas canvas = new RecordingCanvas();
        ITextMeasurer measurer = LayoutHarness.Measurer;

        Assert.Equal(
            "document",
            Assert.Throws<ArgumentNullException>(() => DocumentRenderer.Render(null!, canvas, measurer)).ParamName);
        Assert.Equal(
            "canvas",
            Assert.Throws<ArgumentNullException>(() => DocumentRenderer.Render(document, null!, measurer)).ParamName);
        Assert.Equal(
            "measurer",
            Assert.Throws<ArgumentNullException>(() => DocumentRenderer.Render(document, canvas, null!)).ParamName);
    }

    [Theory]
    [InlineData(0f, 100f, "(Width: 0.000, Height: 100.000)")]
    [InlineData(100f, 0f, "(Width: 100.000, Height: 0.000)")]
    [InlineData(-10f, 100f, "(Width: -10.000, Height: 100.000)")]
    public void RejectsAPageSizeWithoutAreaInEitherDimension(float width, float height, string formattedSize)
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(page =>
        {
            page.Size = new Size(width, height);
            page.Content().Text("nowhere to draw");
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            $"The page size {formattedSize} is not drawable. Both dimensions must be greater than zero.",
            exception.Message);
    }

    [Fact]
    public void RejectsHorizontalMarginsThatConsumeExactlyTheWholeWidth()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(page =>
        {
            page.Size = new Size(100, 100);
            page.Margin = Edges.Symmetric(horizontal: 50, vertical: 0);
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal("The horizontal margins (100.0) leave no room on a page 100.0 points wide.", exception.Message);
    }

    [Fact]
    public void RejectsVerticalMarginsThatConsumeExactlyTheWholeHeight()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(page =>
        {
            page.Size = new Size(100, 100);
            page.Margin = Edges.Symmetric(horizontal: 0, vertical: 50);
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal("The vertical margins (100.0) leave no room on a page 100.0 points tall.", exception.Message);
    }

    [Fact]
    public void ContinuousPagesMeasureVerticalMarginsAgainstTheMaximumHeightRatherThanTheNominalOne()
    {
        // Margins taller than the nominal page would be rejected on a fixed page; a continuous page grows past it.
        Document document = Build(page =>
        {
            page.Size = new Size(200, 100);
            page.Margin = Edges.Symmetric(horizontal: 0, vertical: 60);
            page.IsContinuous = true;
            page.Content().Element(container => container.Child = new FixedElement(10, 10));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(new Size(200, 130), page.Size);
    }

    [Fact]
    public void ContinuousPagesGrowToHoldMarginsBandsAndContentTallerThanTheNominalPage()
    {
        Document document = Build(page =>
        {
            page.Size = new Size(200, 100);
            page.Margin = new Edges(Left: 0, Top: 10, Right: 0, Bottom: 20);
            page.IsContinuous = true;
            page.Header().Element(container => container.Child = new FixedElement(50, 15, Colors.Red));
            page.Content().Element(container => container.Child = new FixedElement(50, 500, Colors.Blue));
            page.Footer().Element(container => container.Child = new FixedElement(50, 25, Colors.Green));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(new Size(200, 570), page.Size);
        Approximately.Equal(10f, Rectangle(page, Colors.Red).Position.Y);
        Approximately.Equal(25f, Rectangle(page, Colors.Blue).Position.Y);
        Approximately.Equal(525f, Rectangle(page, Colors.Green).Position.Y);
    }

    [Fact]
    public void RejectsAHeaderThatDoesNotFitOnThePage()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(page =>
        {
            page.Size = new Size(200, 100);
            page.Header().Element(container => container.Child = new FixedElement(10, 150));
            page.Content().Element(container => container.Child = new FixedElement(10, 10));
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "The page header does not fit in (Width: 200.000, Height: 100.000). Reason: The element requires " +
            "(Width: 10.000, Height: 150.000) but only (Width: 200.000, Height: 100.000) is available.",
            exception.Message);
    }

    [Fact]
    public void RejectsAHeaderThatClaimsMoreThanItWasOffered()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(page =>
        {
            page.Size = new Size(200, 100);
            page.Header().Element(container => container.Child = new OversizedElement(10, 300));
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal("The page header (300.0 points) is taller than the page.", exception.Message);
    }

    [Fact]
    public void RejectsAHeaderThatExpandsToFillThePage()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(page =>
        {
            page.Size = new Size(200, 100);
            page.Margin = Edges.Symmetric(horizontal: 0, vertical: 10);
            page.Header().ExtendVertical().Text("Title");
            page.Content().Text("Body");
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "The page header claimed the entire 80.0 points available, leaving no room for content or footer. " +
            "This usually means it contains an element that expands to fill the space offered to it, such as " +
            "AlignMiddle, AlignBottom or Extend. Give the header an explicit Height, or remove the expanding element.",
            exception.Message);
    }

    [Fact]
    public void RejectsAFooterThatClaimsMoreThanTheHeaderLeft()
    {
        using CultureScope culture = CultureScope.Invariant();
        Document document = Build(page =>
        {
            page.Size = new Size(200, 200);
            page.Header().Element(container => container.Child = new FixedElement(10, 50));
            page.Footer().Element(container => container.Child = new OversizedElement(10, 200));
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "The header (50.0) and footer (200.0) together exceed the 200.0 points available for content.",
            exception.Message);
    }

    [Fact]
    public void AcceptsBandsThatOvershootThePageOnlyWithinTheLayoutTolerance()
    {
        // Every element treats an overshoot below Size.Epsilon as fitting, so a footer may legitimately report a
        // fraction of a thousandth more than it was offered. The page must accept what its own footer accepted.
        Document document = Build(page =>
        {
            page.Size = new Size(200, 200);
            page.Header().Element(container => container.Child = new FixedElement(10, 50, Colors.Red));
            page.Footer().Element(container => container.Child = new FixedElement(10, 150.0005f, Colors.Green));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(50f, Rectangle(page, Colors.Green).Position.Y);
    }

    [Fact]
    public void WrapsADrawingFailureWithThePageItHappenedOn()
    {
        InvalidOperationException failure = new InvalidOperationException("The image could not be decoded.");
        Document document = Document.Create(container =>
        {
            container.Page(page => page.Content().Element(inner => inner.Child = new FixedElement(10, 10)));
            container.Page(page => page.Content().Element(inner => inner.Child = new ThrowingElement(failure)));
        });

        DocumentDrawingException exception =
            Assert.Throws<DocumentDrawingException>(() => LayoutHarness.Render(document));

        Assert.Equal("Drawing page 2 failed. See the inner exception for details.", exception.Message);
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public void LetsALayoutFailureRaisedWhileDrawingPassThroughUnwrapped()
    {
        DocumentLayoutException failure = new DocumentLayoutException("A nested layout could not be resolved.");
        Document document = Build(page => page.Content().Element(inner => inner.Child = new ThrowingElement(failure)));

        Assert.Same(failure, Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document)));
    }

    [Fact]
    public void DoesNotWrapADrawingFailureASecondTime()
    {
        DocumentDrawingException failure = new DocumentDrawingException("A nested document failed to draw.");
        Document document = Build(page => page.Content().Element(inner => inner.Child = new ThrowingElement(failure)));

        Assert.Same(failure, Assert.Throws<DocumentDrawingException>(() => LayoutHarness.Render(document)));
    }

    [Fact]
    public void GivesUpOnAPageRunThatNeverStopsAskingForAnotherPage()
    {
        Document document = Build(page =>
        {
            page.Size = new Size(100, 100);
            page.Content().Element(container => container.Child = new NeverFinishingElement());
        });

        DocumentLayoutException exception =
            Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));

        Assert.Equal(
            "The document exceeded 10000 pages in a single page run, which usually means an element reports " +
            "content remaining but never consumes any space.",
            exception.Message);
    }

    [Fact]
    public void AcceptsAPageRunOfExactlyTheMaximumLength()
    {
        Document document = Build(page =>
        {
            page.Size = new Size(20, 10);
            page.Content().Element(container => container.Child = new SplittableElement(10_000, 10f));
        });

        Assert.Equal(10_000, LayoutHarness.Render(document).Pages.Count);
    }

    [Fact]
    public void CountsPagesQuotingEachAsTheLastThenDrawsWithTheSettledTotal()
    {
        PageContextRecorder recorder = new PageContextRecorder();
        Document document = Build(page =>
        {
            page.Size = new Size(200, 100);
            page.Background().Element(container => container.Child = recorder);
            page.Content().Element(container => container.Child = new SplittableElement(3, 100f));
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
        OscillatingElement content = new OscillatingElement();
        Document document = Build(page =>
        {
            page.Size = new Size(200, 100);
            page.Content().Element(container => container.Child = content);
        });

        RecordingCanvas canvas = LayoutHarness.Render(document);

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
            page.Size = new Size(200, 200);
            page.Background().Element(container => container.Child = new SplittableElement(1, 5f, width: 13));
            page.Header().Element(container => container.Child = new SplittableElement(1, 20f, width: 11));
            page.Content().Element(container => container.Child = new SplittableElement(3, 160f, width: 15));
            page.Footer().Element(container => container.Child = new SplittableElement(1, 20f, width: 12));
            page.Foreground().Element(container => container.Child = new SplittableElement(1, 5f, width: 14));
        });

        RecordingCanvas canvas = LayoutHarness.Render(document);

        Assert.Equal(3, canvas.Pages.Count);
        foreach (RecordedPage page in canvas.Pages)
        {
            float[] widthsInDrawOrder = page.Operations
                .OfType<RectangleOperation>()
                .Where(operation => operation.Color == Colors.Blue)
                .Select(operation => operation.Size.Width)
                .ToArray();

            Assert.Equal(new[] { 13f, 11f, 15f, 12f, 14f }, widthsInDrawOrder);
        }
    }

    [Fact]
    public void ShowsAShowOnceHeaderOnlyOnTheFirstPageOfTheFinalOutput()
    {
        // The counting passes have already drawn the header once, so the drawing pass must start from a full
        // reset; the per-page reset in between must leave the "already shown" flag alone.
        Document document = Build(page =>
        {
            page.Size = new Size(200, 200);
            page.Header().ShowOnce().Element(container => container.Child = new FixedElement(50, 20, Colors.Red));
            page.Content().Element(container => container.Child = new SplittableElement(3, 150f));
        });

        RecordingCanvas canvas = LayoutHarness.Render(document);

        int[] headersPerPage = canvas.Pages
            .Select(page => page.Operations.OfType<RectangleOperation>().Count(operation => operation.Color == Colors.Red))
            .ToArray();
        Assert.Equal(new[] { 1, 0, 0 }, headersPerPage);
    }

    [Fact]
    public void PaintsThePageBackgroundColourAcrossTheWholeSheetIgnoringMargins()
    {
        Document document = Build(page =>
        {
            page.Size = new Size(200, 300);
            page.Margin = Edges.All(20);
            page.BackgroundColor = Colors.Amber;
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);
        RectangleOperation fill = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(Colors.Amber.Base, fill.Color);
        Approximately.Equal(Position.Zero, fill.Position);
        Approximately.Equal(new Size(200, 300), fill.Size);
    }

    [Fact]
    public void PaintsNothingForATransparentPageBackground()
    {
        Document document = Build(page =>
        {
            page.Size = new Size(200, 300);
            page.BackgroundColor = Colors.Transparent;
        });

        Assert.Empty(Assert.Single(LayoutHarness.Render(document).Pages).Operations);
    }

    [Fact]
    public void PlacesEveryBandRelativeToTheMarginsAndTheForegroundAtThePageCorner()
    {
        Document document = Build(page =>
        {
            page.Size = new Size(200, 300);
            page.Margin = Edges.All(20);
            page.Header().Element(container => container.Child = new FixedElement(50, 30, Colors.Red));
            page.Content().Element(container => container.Child = new FixedElement(50, 10, Colors.Blue));
            page.Footer().Element(container => container.Child = new FixedElement(50, 30, Colors.Green));
            page.Foreground().Element(container => container.Child = new FixedElement(200, 300, Colors.Cyan));
        });

        RecordedPage page = Assert.Single(LayoutHarness.Render(document).Pages);

        Approximately.Equal(new Position(20, 20), Rectangle(page, Colors.Red).Position);
        Approximately.Equal(new Position(20, 50), Rectangle(page, Colors.Blue).Position);
        Approximately.Equal(new Position(20, 250), Rectangle(page, Colors.Green).Position);
        Approximately.Equal(Position.Zero, Rectangle(page, Colors.Cyan).Position);
    }

    private static RectangleOperation Rectangle(RecordedPage page, Color color) =>
        page.Operations.OfType<RectangleOperation>().Single(operation => operation.Color == color);
}
