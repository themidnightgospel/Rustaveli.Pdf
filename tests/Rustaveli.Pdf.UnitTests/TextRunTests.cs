namespace Rustaveli.Pdf.UnitTests;

public class TextRunTests
{
    [Fact]
    public void ResolvesToItsLiteralText()
    {
        TextRun span = new TextRun { Text = "hello" };

        Assert.Equal("hello", span.Resolve(new Pagination()));
    }

    [Fact]
    public void DynamicTextWinsOverLiteralTextAndSeesThePageBeingDrawn()
    {
        Pagination page = new Pagination { CurrentPage = 3, TotalPages = 12 };
        TextRun span = new TextRun
        {
            Text = "ignored",
            DynamicText = context => $"{context.CurrentPage} of {context.TotalPages}",
        };

        Assert.Equal("3 of 12", span.Resolve(page));
    }

    [Fact]
    public void ResolvesToAnEmptyStringWhenItHasNoContent()
    {
        Assert.Equal(string.Empty, new TextRun().Resolve(new Pagination()));
    }

    [Fact]
    public void InheritsTheSurroundingStyleUnchangedWithoutAnOverride()
    {
        TypeStyle inherited = TypeStyle.Default.WithPointSize(20);

        Assert.Same(inherited, new TextRun().ResolveStyle(inherited));
    }

    [Fact]
    public void AppliesItsOverrideOnTopOfTheSurroundingStyle()
    {
        TextRun span = new TextRun { StyleOverride = style => style.Bold() };

        TypeStyle resolved = span.ResolveStyle(TypeStyle.Default.WithPointSize(20));

        Assert.Equal(TypeStyle.Default.WithPointSize(20).Bold(), resolved);
    }
}
