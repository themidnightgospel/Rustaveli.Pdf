using CsCheck;
using Size = Rustaveli.Pdf.Primitives.Extent;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// Invariants every layout must keep, checked against hundreds of randomly composed documents. Each property names
/// a class of defect this engine has had or could have: text lost or repeated at a page break, output that depends
/// on what was measured before, content escaping the page, measurement that changes state.
/// </summary>
public class LayoutPropertyTests
{
    private const long Iterations = 300;

    private static readonly Size PageSize = new Size(400, 500);

    private static (Document Document, string Written) Build(TreeNode tree)
    {
        TreeComposer composer = new TreeComposer();
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = PageSize;
            page.Margins = Sides.All(20);
            page.DefaultType = TypeStyle.Default.WithPointSize(8);
            composer.Compose(page.Body(), tree);
        }));

        return (document, composer.WrittenText);
    }

    private static string Drawn(RecordingSurface canvas) =>
        string.Concat(canvas.Pages.SelectMany(page => page.Texts).Select(text => text.Text)).Replace(" ", string.Empty);

    private static string Sorted(string text) => new string(text.OrderBy(character => character).ToArray());

    [Fact]
    public void EveryCharacterOfTextIsDrawnExactlyOnce()
    {
        TreeGenerator.Tree.Sample(tree =>
        {
            (Document document, string written) = Build(tree);

            string drawn = Drawn(LayoutHarness.Render(document));

            // Compared as multisets: row items and table cells legitimately interleave their text across pages,
            // but a line dropped or repeated at a page break changes the counts.
            Assert.Equal(Sorted(written), Sorted(drawn));
        }, iter: Iterations);
    }

    [Fact]
    public void RenderingTheSameDocumentTwiceProducesTheSameOperations()
    {
        TreeGenerator.Tree.Sample(tree =>
        {
            RecordingSurface first = LayoutHarness.Render(Build(tree).Document);
            RecordingSurface second = LayoutHarness.Render(Build(tree).Document);

            Assert.Equal(first.Pages.Count, second.Pages.Count);
            for (int index = 0; index < first.Pages.Count; index++)
                Assert.Equal(first.Pages[index].Operations, second.Pages[index].Operations);
        }, iter: Iterations);
    }

    [Fact]
    public void NothingIsDrawnOutsideThePage()
    {
        TreeGenerator.Tree.Sample(tree =>
        {
            RecordingSurface canvas = LayoutHarness.Render(Build(tree).Document);

            foreach (RecordedPage page in canvas.Pages)
            {
                foreach (RectangleOperation rectangle in page.Operations.OfType<RectangleOperation>())
                {
                    Assert.InRange(rectangle.Bounds.Left, -Size.Epsilon, PageSize.Width + Size.Epsilon);
                    Assert.InRange(rectangle.Bounds.Right, -Size.Epsilon, PageSize.Width + Size.Epsilon);
                    Assert.InRange(rectangle.Bounds.Top, -Size.Epsilon, PageSize.Height + Size.Epsilon);
                    Assert.InRange(rectangle.Bounds.Bottom, -Size.Epsilon, PageSize.Height + Size.Epsilon);
                }

                foreach (TextOperation text in page.Texts)
                {
                    Assert.InRange(text.Position.X, -Size.Epsilon, PageSize.Width + Size.Epsilon);
                    Assert.InRange(text.Position.Y, -Size.Epsilon, PageSize.Height + Size.Epsilon);
                }
            }
        }, iter: Iterations);
    }

    [Fact]
    public void MeasuringChangesNothing()
    {
        TreeGenerator.Tree.Sample(tree =>
        {
            TreeComposer composer = new TreeComposer();
            Block root = LayoutHarness.Build(container => composer.Compose(container, tree));
            Size space = new Size(360, 460);

            Fit first = LayoutHarness.Measure(root, space);
            Fit second = LayoutHarness.Measure(root, space);
            RecordedPage drawnAfterMeasuring = LayoutHarness.Draw(root, first.IsDeferred || first.IsNothing ? space : first.Size);

            Block fresh = LayoutHarness.Build(container => new TreeComposer().Compose(container, tree));
            RecordedPage drawnFresh = LayoutHarness.Draw(fresh, first.IsDeferred || first.IsNothing ? space : first.Size);

            Assert.Equal(first, second);
            Assert.Equal(drawnFresh.Operations, drawnAfterMeasuring.Operations);
        }, iter: Iterations);
    }
}
