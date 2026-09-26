using Rustaveli.Pdf.Skia;
using Rustaveli.Pdf.Text;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// How a text style resolves to a typeface.
/// </summary>
public class SkiaFontProviderTests
{
    private static readonly TextStyle Arial = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(12);

    [Fact]
    public void TheTypefaceFollowsTheStylesWeightAndSlant()
    {
        using SkiaFontProvider fonts = new SkiaFontProvider();

        SKTypeface regular = fonts.GetTypeface(Arial);
        SKTypeface bold = fonts.GetTypeface(Arial.Bold());
        SKTypeface italic = fonts.GetTypeface(Arial.Italic());

        Assert.True(bold.FontWeight > regular.FontWeight, $"Bold resolved to weight {bold.FontWeight}, regular to {regular.FontWeight}.");
        Assert.Equal(SKFontStyleSlant.Upright, regular.FontSlant);
        Assert.NotEqual(SKFontStyleSlant.Upright, italic.FontSlant);
    }

    [Fact]
    public void OneTypefaceServesEverySizeOfAStyle()
    {
        using SkiaFontProvider fonts = new SkiaFontProvider();

        SKTypeface small = fonts.GetTypeface(Arial);

        Assert.Same(small, fonts.GetTypeface(Arial.FontSizeOf(48)));
        Assert.Same(small, fonts.GetFont(Arial.FontSizeOf(48)).Typeface);
    }
}
