namespace Rustaveli.Pdf.UnitTests;

public class TypefaceFallbacksTests
{
    private static readonly TypeStyle Base = TypeStyle.Default;

    [Fact]
    public void AStyleHasNoFallbacksOfItsOwnUntilGiven()
    {
        Assert.Empty(Base.Fallbacks);
        Assert.Same(TypefaceFallbacks.None, Base.FallbackTypefaces);
        Assert.Equal("none", Base.FallbackTypefaces.ToString());
    }

    [Fact]
    public void WithTypefaceSetsTheFallbacksInOrder()
    {
        TypeStyle style = Base.WithTypeface("Inter", "Noto Sans Georgian", "Noto Sans CJK");

        Assert.Equal("Inter", style.Typeface);
        Assert.Equal(["Noto Sans Georgian", "Noto Sans CJK"], style.Fallbacks);
        Assert.Equal("Noto Sans Georgian, Noto Sans CJK", style.FallbackTypefaces.ToString());
    }

    [Fact]
    public void NamingTheTypefaceAgainReplacesTheFallbacks()
    {
        TypeStyle style = Base.WithTypeface("Inter", "Noto Sans Georgian").WithTypeface("Inter");

        Assert.Empty(style.Fallbacks);
        Assert.Equal(Base.WithTypeface("Inter"), style);
    }

    [Fact]
    public void BlankFallbacksAreLeftOutAndNamesTrimmed() =>
        Assert.Equal(["Noto Sans"], Base.WithTypeface("Inter", " ", "", " Noto Sans ").Fallbacks);

    [Fact]
    public void FallbacksAreComparedByValueIgnoringCase()
    {
        TypeStyle first = Base.WithTypeface("Inter", "Noto Sans", "Noto Serif");
        TypeStyle second = Base.WithTypeface("Inter", "noto sans", "NOTO SERIF");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DifferentFallbacksMakeDifferentStyles()
    {
        Assert.NotEqual(Base.WithTypeface("Inter", "Noto Sans"), Base.WithTypeface("Inter", "Noto Serif"));
        Assert.NotEqual(Base.WithTypeface("Inter", "Noto Sans"), Base.WithTypeface("Inter", "Noto Sans", "Noto Serif"));
        Assert.NotEqual(Base.WithTypeface("Inter", "A", "B"), Base.WithTypeface("Inter", "B", "A"));
        Assert.False(Base.WithTypeface("Inter", "A").FallbackTypefaces.Equals((object?)null));
        Assert.False(Base.WithTypeface("Inter", "A").FallbackTypefaces.Equals("A"));
    }

    [Fact]
    public void TheFallbackListCannotBeNull() =>
        Assert.Equal("fallbacks", Assert.Throws<ArgumentNullException>(() => Base.WithTypeface("Inter", null!)).ParamName);

    [Fact]
    public void TheRunComposerSetsTheTypefaceAndItsFallbacks()
    {
        Block root = LayoutHarness.Build(frame => frame.Text(text => text.Run("x").Typeface("Inter", "Noto Sans")));

        TypeStyle style = Assert.Single(LayoutHarness.Render(root, new Extent(200, 200)).Texts).Style;

        Assert.Equal("Inter", style.Typeface);
        Assert.Equal(["Noto Sans"], style.Fallbacks);
    }
}
