namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// The parts content can play in a tagged document, named in words and written as PDF's standard structure types.
/// </summary>
public class ContentTagTests
{
    public static TheoryData<ContentTag, string> Named => new TheoryData<ContentTag, string>
    {
        { ContentTag.Section, "Sect" },
        { ContentTag.Article, "Art" },
        { ContentTag.Division, "Div" },
        { ContentTag.BlockQuote, "BlockQuote" },
        { ContentTag.Caption, "Caption" },
        { ContentTag.Index, "Index" },
        { ContentTag.Contents, "TOC" },
        { ContentTag.ContentsEntry, "TOCI" },
        { ContentTag.Paragraph, "P" },
        { ContentTag.List, "L" },
        { ContentTag.ListItem, "LI" },
        { ContentTag.ListLabel, "Lbl" },
        { ContentTag.ListBody, "LBody" },
        { ContentTag.Table, "Table" },
        { ContentTag.Quote, "Quote" },
        { ContentTag.Code, "Code" },
        { ContentTag.Note, "Note" },
        { ContentTag.Span, "Span" },
    };

    [Theory]
    [MemberData(nameof(Named))]
    public void EachTagIsAStandardStructureType(ContentTag tag, string role)
    {
        Assert.Equal(role, tag.Role);
        Assert.Equal(role, tag.ToString());
        Assert.Null(tag.AlternateText);
        Assert.Null(tag.Expansion);
    }

    [Theory]
    [InlineData(1, "H1")]
    [InlineData(2, "H2")]
    [InlineData(6, "H6")]
    public void HeadingsAreNumberedByLevel(int level, string role) => Assert.Equal(role, ContentTag.Heading(level).Role);

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void HeadingLevelsRunFromOneToSix(int level) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ContentTag.Heading(level));

    [Fact]
    public void FiguresAndFormulasAreReadAsTheirAlternateText()
    {
        ContentTag figure = ContentTag.Figure("A bar chart of sales");
        ContentTag formula = ContentTag.Formula("E equals m c squared");

        Assert.Equal(("Figure", "A bar chart of sales"), (figure.Role, figure.AlternateText));
        Assert.Equal(("Formula", "E equals m c squared"), (formula.Role, formula.AlternateText));
        Assert.Null(figure.Expansion);
    }

    [Fact]
    public void AnAbbreviationIsASpanReadAsItsExpansion()
    {
        ContentTag abbreviation = ContentTag.Abbreviation("for example");

        Assert.Equal(("Span", "for example"), (abbreviation.Role, abbreviation.Expansion));
        Assert.Null(abbreviation.AlternateText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void WhatTheReaderHearsCannotBeBlank(string? text)
    {
        Assert.ThrowsAny<ArgumentException>(() => ContentTag.Figure(text!));
        Assert.ThrowsAny<ArgumentException>(() => ContentTag.Formula(text!));
        Assert.ThrowsAny<ArgumentException>(() => ContentTag.Abbreviation(text!));
    }
}
