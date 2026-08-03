namespace Rustaveli.Pdf.UnitTests;

public class EnsureSpaceTests
{
    [Fact]
    public void DefersWhenTooLittleRoomRemains()
    {
        EnsureSpaceElement element = new EnsureSpaceElement { MinHeight = 80, Child = new FixedElement(10, 10) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 50));

        Assert.True(plan.IsWrap);
        Assert.Contains("80", plan.WrapReason);
    }

    [Fact]
    public void ProceedsWhenEnoughRoomRemains()
    {
        EnsureSpaceElement element = new EnsureSpaceElement { MinHeight = 40, Child = new FixedElement(10, 10) };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 50)).IsFullRender);
    }

    [Fact]
    public void StopsDemandingHeadroomOnceTheContentHasStarted()
    {
        // The requirement is about where content begins, not about every page it continues onto.
        EnsureSpaceElement element = new EnsureSpaceElement
        {
            MinHeight = 80,
            Child = new SplittableElement(unitCount: 6, unitHeight: 20)
        };

        LayoutHarness.Draw(element, new Size(200, 100));

        Assert.False(LayoutHarness.Measure(element, new Size(200, 40)).IsWrap);
    }

    [Fact]
    public void MovesAHeadingToTheNextPageRatherThanStrandingIt()
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Size(200, 100);
            page.Content().Column(column =>
            {
                column.Item().Element(inner => inner.Child = new FixedElement(10, 70, Colors.Blue));
                column.Item().EnsureSpace(50).Element(inner => inner.Child = new FixedElement(10, 10, Colors.Red));
            });
        }));

        RecordingCanvas canvas = LayoutHarness.Render(document);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.DoesNotContain(canvas.Page(1).Operations.OfType<RectangleOperation>(), r => r.Color == Colors.Red);
        Assert.Contains(canvas.Page(2).Operations.OfType<RectangleOperation>(), r => r.Color == Colors.Red);
    }
}
