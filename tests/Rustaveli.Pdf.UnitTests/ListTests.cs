namespace Rustaveli.Pdf.UnitTests;

public class ListTests
{
    private static Element BuildList(Action<ListDescriptor> compose) =>
        LayoutHarness.Build(container => container.List(compose));

    [Fact]
    public void BulletsEveryItemByDefault()
    {
        Element root = BuildList(list =>
        {
            list.Item().Text("alpha");
            list.Item().Text("beta");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 200));
        List<TextOperation> markers = page.Texts.Where(t => t.Text == "•").ToList();

        Assert.Equal(2, markers.Count);
    }

    [Fact]
    public void NumbersItemsWhenOrdered()
    {
        Element root = BuildList(list =>
        {
            list.Ordered();
            list.Item().Text("alpha");
            list.Item().Text("beta");
            list.Item().Text("gamma");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 200));

        Assert.Contains("1.", page.Content);
        Assert.Contains("3.", page.Content);
    }

    [Theory]
    [InlineData(ListMarker.LowerLetter, "a.")]
    [InlineData(ListMarker.UpperLetter, "A.")]
    [InlineData(ListMarker.LowerRoman, "i.")]
    [InlineData(ListMarker.UpperRoman, "I.")]
    public void SupportsAlternativeOrderedStyles(ListMarker marker, string expectedFirstMarker)
    {
        Element root = BuildList(list =>
        {
            list.Ordered(marker);
            list.Item().Text("only");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 200));

        Assert.Contains(expectedFirstMarker, page.Content);
    }

    [Fact]
    public void ContinuesNumberingAcrossAPageBreak()
    {
        // Markers are resolved from position at compose time, so a break cannot restart the count.
        Element root = BuildList(list =>
        {
            list.Ordered();

            for (int index = 0; index < 6; index++)
                list.Item().Element(inner => inner.Child = new FixedElement(10, 30));
        });

        Size space = new Size(200, 60);

        RecordedPage firstPage = LayoutHarness.Draw(root, space);
        RecordedPage secondPage = LayoutHarness.Draw(root, space);

        Assert.Contains("1.", firstPage.Content);
        Assert.Contains("2.", firstPage.Content);
        Assert.Contains("3.", secondPage.Content);
        Assert.DoesNotContain("1.", secondPage.Content);
    }

    [Fact]
    public void RomanNumeralsCompose()
    {
        Element root = BuildList(list =>
        {
            list.Ordered(ListMarker.UpperRoman);

            for (int index = 0; index < 9; index++)
                list.Item().Text("item");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 500));

        Assert.Contains("IV.", page.Content);
        Assert.Contains("IX.", page.Content);
    }

    [Fact]
    public void LettersContinueBeyondTheAlphabet()
    {
        Element root = BuildList(list =>
        {
            list.Ordered(ListMarker.UpperLetter);

            for (int index = 0; index < 27; index++)
                list.Item().Text("item");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 900));

        Assert.Contains("Z.", page.Content);
        Assert.Contains("AA.", page.Content);
    }

    [Fact]
    public void IndentsContentPastTheMarkerGutter()
    {
        Element root = BuildList(list =>
        {
            list.MarkerWidth(30);
            list.Item().Element(inner => inner.Child = new FixedElement(10, 10, Colors.Red));
        });

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 200));
        RectangleOperation content = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == Colors.Red);

        Approximately.Equal(30f, content.Position.X);
    }
}
