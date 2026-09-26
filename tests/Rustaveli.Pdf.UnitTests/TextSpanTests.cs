namespace Rustaveli.Pdf.UnitTests;

public class TextSpanTests
{
    [Fact]
    public void ResolvesToItsLiteralText()
    {
        TextSpan span = new TextSpan { Text = "hello" };

        Assert.Equal("hello", span.Resolve(new PageContext()));
    }

    [Fact]
    public void DynamicTextWinsOverLiteralTextAndSeesThePageBeingDrawn()
    {
        PageContext page = new PageContext { CurrentPage = 3, TotalPages = 12 };
        TextSpan span = new TextSpan
        {
            Text = "ignored",
            DynamicText = context => $"{context.CurrentPage} of {context.TotalPages}",
        };

        Assert.Equal("3 of 12", span.Resolve(page));
    }

    [Fact]
    public void ResolvesToAnEmptyStringWhenItHasNoContent()
    {
        Assert.Equal(string.Empty, new TextSpan().Resolve(new PageContext()));
    }

    [Fact]
    public void InheritsTheSurroundingStyleUnchangedWithoutAnOverride()
    {
        TextStyle inherited = TextStyle.Default.FontSizeOf(20);

        Assert.Same(inherited, new TextSpan().ResolveStyle(inherited));
    }

    [Fact]
    public void AppliesItsOverrideOnTopOfTheSurroundingStyle()
    {
        TextSpan span = new TextSpan { StyleOverride = style => style.Bold() };

        TextStyle resolved = span.ResolveStyle(TextStyle.Default.FontSizeOf(20));

        Assert.Equal(TextStyle.Default.FontSizeOf(20).Bold(), resolved);
    }
}
