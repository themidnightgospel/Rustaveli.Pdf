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
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(200f, 200f);
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 500f);
            });
        });
        DocumentLayoutException ex = Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));
        Assert.Contains("empty page", ex.Message);
    }

    [Fact]
    public void ThrowsWhenMarginsLeaveNoRoom()
    {
        Document document = Build(delegate(PageDescriptor page)
        {
            page.Size = new Size(100f, 100f);
            page.Margin = Edges.All(60f);
            page.Content().Element(delegate(IContainer container)
            {
                container.Child = new FixedElement(10f, 10f);
            });
        });
        Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));
    }

    [Fact]
    public void ThrowsWhenTheHeaderAndFooterFillThePage()
    {
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
        Assert.Throws<DocumentLayoutException>(() => LayoutHarness.Render(document));
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
}
