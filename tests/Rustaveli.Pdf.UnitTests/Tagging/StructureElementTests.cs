using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests.Tagging;

/// <summary>
/// One element of a document's structure: what it groups, what it illustrates, and the language it is in.
/// </summary>
public class StructureElementTests
{
    [Fact]
    public void AnElementStartsEmptySpanningOneCell()
    {
        StructureElement parent = new StructureElement("Document", null);
        StructureElement element = new StructureElement("TD", parent);

        Assert.Equal("TD", element.Role);
        Assert.Same(parent, element.Parent);
        Assert.Empty(element.Kids);
        Assert.Equal((1, 1), (element.RowSpan, element.ColumnSpan));
        Assert.Null(element.Scope);
        Assert.Null(element.AlternateText);
        Assert.Null(element.Expansion);
        Assert.Null(element.Language);
    }

    [Theory]
    [InlineData("Figure", true)]
    [InlineData("Formula", true)]
    [InlineData("P", false)]
    [InlineData("Span", false)]
    [InlineData("Document", false)]
    public void OnlyFiguresAndFormulasIllustrate(string role, bool illustrates) =>
        Assert.Equal(illustrates, new StructureElement(role, null).IsIllustration);

    [Theory]
    [InlineData("Document")]
    [InlineData("Part")]
    [InlineData("Art")]
    [InlineData("Sect")]
    [InlineData("Div")]
    [InlineData("BlockQuote")]
    [InlineData("TOC")]
    [InlineData("Index")]
    [InlineData("L")]
    [InlineData("LI")]
    [InlineData("Table")]
    [InlineData("THead")]
    [InlineData("TBody")]
    [InlineData("TFoot")]
    [InlineData("TR")]
    public void GroupingElementsHoldOtherElements(string role) =>
        Assert.True(new StructureElement(role, null).IsGrouping);

    [Theory]
    [InlineData("P")]
    [InlineData("H1")]
    [InlineData("TD")]
    [InlineData("TH")]
    [InlineData("Lbl")]
    [InlineData("LBody")]
    [InlineData("Span")]
    [InlineData("Link")]
    [InlineData("Caption")]
    [InlineData("TOCI")]
    [InlineData("Figure")]
    public void OtherElementsHoldTheirTextThemselves(string role) =>
        Assert.False(new StructureElement(role, null).IsGrouping);

    [Fact]
    public void TheLanguageInForceIsTheNearestGiven()
    {
        StructureElement document = new StructureElement("Document", null);
        StructureElement section = new StructureElement("Sect", document) { Language = "ka" };
        StructureElement paragraph = new StructureElement("P", section);
        StructureElement span = new StructureElement("Span", paragraph) { Language = "en" };

        Assert.Null(document.EffectiveLanguage);
        Assert.Equal("ka", section.EffectiveLanguage);
        Assert.Equal("ka", paragraph.EffectiveLanguage);
        Assert.Equal("en", span.EffectiveLanguage);
    }
}
