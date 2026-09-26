using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// How a text style resolves to a typeface.
/// </summary>
public class SkiaFontProviderTests
{
    private static readonly TypeStyle Sans = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(12);

    [Fact]
    public void TheTypefaceFollowsTheStylesWeightAndSlant()
    {
        using SkiaFontProvider fonts = TestFonts.NewProvider();

        SKTypeface regular = fonts.GetTypeface(Sans);
        SKTypeface bold = fonts.GetTypeface(Sans.Bold());
        SKTypeface italic = fonts.GetTypeface(Sans.Italic());

        Assert.True(bold.FontWeight > regular.FontWeight, $"Bold resolved to weight {bold.FontWeight}, regular to {regular.FontWeight}.");
        Assert.Equal(SKFontStyleSlant.Upright, regular.FontSlant);
        Assert.NotEqual(SKFontStyleSlant.Upright, italic.FontSlant);
    }

    [Fact]
    public void OneTypefaceServesEverySizeOfAStyle()
    {
        using SkiaFontProvider fonts = TestFonts.NewProvider();

        SKTypeface small = fonts.GetTypeface(Sans);

        Assert.Same(small, fonts.GetTypeface(Sans.WithPointSize(48)));
        Assert.Same(small, fonts.GetFont(Sans.WithPointSize(48)).Typeface);
    }
}
