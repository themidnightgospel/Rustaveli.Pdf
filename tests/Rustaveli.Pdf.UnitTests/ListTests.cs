namespace Rustaveli.Pdf.UnitTests;

public class ListTests
{
    private static Block BuildList(Action<ListComposer> compose) =>
        LayoutHarness.Build(container => container.List(compose));

    [Fact]
    public void BulletsEveryItemByDefault()
    {
        Block root = BuildList(list =>
        {
            list.Add().Text("alpha");
            list.Add().Text("beta");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));
        List<TextOperation> markers = page.Texts.Where(t => t.Text == "•").ToList();

        Assert.Equal(2, markers.Count);
    }

    [Fact]
    public void NumbersItemsWhenOrdered()
    {
        Block root = BuildList(list =>
        {
            list.Numbered();
            list.Add().Text("alpha");
            list.Add().Text("beta");
            list.Add().Text("gamma");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));

        Assert.Contains("1.", page.Content);
        Assert.Contains("3.", page.Content);
    }

    [Theory]
    [InlineData(ListNumbering.LowerAlpha, "a.")]
    [InlineData(ListNumbering.UpperAlpha, "A.")]
    [InlineData(ListNumbering.LowerRoman, "i.")]
    [InlineData(ListNumbering.UpperRoman, "I.")]
    public void SupportsAlternativeOrderedStyles(ListNumbering marker, string expectedFirstMarker)
    {
        Block root = BuildList(list =>
        {
            list.Numbered(marker);
            list.Add().Text("only");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));

        Assert.Contains(expectedFirstMarker, page.Content);
    }

    [Fact]
    public void ContinuesNumberingAcrossAPageBreak()
    {
        // Markers are resolved from position at compose time, so a break cannot restart the count.
        Block root = BuildList(list =>
        {
            list.Numbered();

            for (int index = 0; index < 6; index++)
                list.Add().Compose(inner => inner.Child = new FixedBlock(10, 30));
        });

        Extent space = new Extent(200, 60);

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
        Block root = BuildList(list =>
        {
            list.Numbered(ListNumbering.UpperRoman);

            for (int index = 0; index < 9; index++)
                list.Add().Text("item");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 500));

        Assert.Contains("IV.", page.Content);
        Assert.Contains("IX.", page.Content);
    }

    [Fact]
    public void LettersContinueBeyondTheAlphabet()
    {
        Block root = BuildList(list =>
        {
            list.Numbered(ListNumbering.UpperAlpha);

            for (int index = 0; index < 27; index++)
                list.Add().Text("item");
        });

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 900));

        Assert.Contains("Z.", page.Content);
        Assert.Contains("AA.", page.Content);
    }

    [Fact]
    public void IndentsContentPastTheMarkerGutter()
    {
        Block root = BuildList(list =>
        {
            list.MarkerIndent(30);
            list.Add().Compose(inner => inner.Child = new FixedBlock(10, 10, TestInks.Red));
        });

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));
        RectangleOperation content = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(30f, content.Position.X);
    }
}
