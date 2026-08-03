namespace Rustaveli.Pdf.UnitTests;

public class LayersTests
{
    [Fact]
    public void OverlayLayersRepeatOnEveryPage()
    {
        // A watermark accompanies its content onto every page. Its text tracks how much of itself it has drawn,
        // so without a per-page reset it would be consumed on page one and trail off mid-word on page two.
        LayersElement element = new LayersElement();

        Layer primary = new Layer { IsPrimary = true, Child = new SplittableElement(unitCount: 4, unitHeight: 20, width: 200) };
        Layer overlay = new Layer();
        ((IContainer)overlay).Text("mark");

        element.Layers.Add(primary);
        element.Layers.Add(overlay);

        Size space = new Size(200, 40);

        RecordedPage firstPage = LayoutHarness.Draw(element, space);
        RecordedPage secondPage = LayoutHarness.Draw(element, space);

        Assert.Equal("mark", firstPage.Content);
        Assert.Equal("mark", secondPage.Content);
    }
}
