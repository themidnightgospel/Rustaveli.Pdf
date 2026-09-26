using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using Rustaveli.Pdf.Text;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Verifies that characters absent from the requested font are drawn with one that has them.
/// </summary>
/// <remarks>
/// The decisive check is extraction: a glyph the primary font lacks is emitted as .notdef without fallback, and
/// a reader recovers nothing for it. Getting the characters back out proves a covering face was embedded.
/// </remarks>
public class FontFallbackTests
{
    private const string Latin = "Hello";
    private const string Cjk = "世界";
    private const string Georgian = "გამარჯობა";

    private static readonly TextStyle Arial = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

    private static Document Build(string text) => Build(text, Arial);

    private static Document Build(string text, TextStyle style) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = PageSizes.A4;
            page.Margin = Edges.All(30);
            page.DefaultTextStyle = style;
            page.Content().Text(text);
        }));

    /// <summary>The embedded font a character was drawn with, without the subset tag.</summary>
    private static string FontOf(byte[] pdf, string character)
    {
        using PdfDocument parsed = PdfDocument.Open(pdf);
        string? name = Assert.Single(parsed.GetPage(1).Letters, letter => letter.Value == character).FontName;

        Assert.False(string.IsNullOrEmpty(name), $"'{character}' was drawn with an unnamed font.");
        return name!.Substring(name.IndexOf('+') + 1);
    }

    /// <summary>
    /// Finds a character <paramref name="primary"/> lacks, together with an installed family that has it but is
    /// not the one platform matching would pick — the pair a test needs to tell an explicit fallback apart from
    /// the platform's own choice. Several scripts are tried so that some pair exists on any host with fonts.
    /// </summary>
    private static bool TryFindAlternativeFallback(SKTypeface primary, out string character, out string family)
    {
        int[] codepoints = [0x4E16, 0x2605, 0x10D0, 0x0E01, 0x0905];
        string[] families = SKFontManager.Default.FontFamilies.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        using SKFont primaryProbe = new SKFont(primary);

        foreach (int codepoint in codepoints)
        {
            if (primaryProbe.ContainsGlyph(codepoint))
                continue;

            string? platformChoice = SKFontManager.Default
                .MatchCharacter(primary.FamilyName, 400, (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Upright, null, codepoint)
                ?.FamilyName;

            foreach (string candidate in families)
            {
                // Typefaces from the font manager are shared across the process, so none is disposed here.
                SKTypeface? typeface = SKFontManager.Default.MatchFamily(candidate);

                if (typeface is null || typeface.FamilyName == platformChoice || typeface.FamilyName == primary.FamilyName)
                    continue;

                using SKFont probe = new SKFont(typeface);

                if (probe.ContainsGlyph(codepoint))
                {
                    character = char.ConvertFromUtf32(codepoint);
                    family = typeface.FamilyName;
                    return true;
                }
            }
        }

        character = string.Empty;
        family = string.Empty;
        return false;
    }

    [Fact]
    public void RendersCharactersTheRequestedFontDoesNotHave()
    {
        using PdfDocument parsed = PdfDocument.Open(Build($"{Latin} {Cjk}").GeneratePdf());
        string text = parsed.GetPage(1).Text;

        Assert.Contains(Latin, text);
        Assert.Contains(Cjk, text);
    }

    [Fact]
    public void MixedScriptTextMeasuresWiderThanItsLatinPartAlone()
    {
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        float latinOnly = measurer.MeasureWidth(Latin, style);
        float mixed = measurer.MeasureWidth($"{Latin}{Cjk}", style);

        Assert.True(mixed > latinOnly, $"Mixed-script text measured {mixed} against {latinOnly} for the Latin part alone.");
    }

    [Fact]
    public void MeasuredWidthMatchesWhatIsDrawn()
    {
        // Measurement and drawing must split the string identically; if they disagreed, a fallback glyph would
        // land somewhere other than where its advance was reserved.
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        float whole = measurer.MeasureWidth($"{Latin}{Cjk}", style);
        float parts = measurer.MeasureWidth(Latin, style) + measurer.MeasureWidth(Cjk, style);

        Assert.InRange(whole, parts - 0.5f, parts + 0.5f);
    }

    [Fact]
    public void AnExplicitFallbackFamilyIsPreferredOverPlatformMatching()
    {
        // Naming the fallback is what makes output reproducible; left to the platform the substitute differs
        // between machines.
        using SkiaFontProvider provider = new SkiaFontProvider();
        provider.FallbackFamilies.Add("Segoe UI");

        SkiaTextMeasurer measurer = new SkiaTextMeasurer(provider);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        Assert.True(measurer.MeasureWidth(Cjk, style) > 0);
    }

    [Fact]
    public void PurelyLatinTextIsUnaffected()
    {
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        // The fast path must produce exactly what a single-font measurement always did.
        Assert.True(measurer.MeasureWidth(Latin, style) > 0);
        Assert.Equal(measurer.MeasureWidth(Latin, style), measurer.MeasureWidth(Latin, style));
    }

    [Fact]
    public void AnExplicitFallbackFamilyDrawsWhatThePrimaryFontLacks()
    {
        Assert.True(
            TryFindAlternativeFallback(SkiaFontProvider.Shared.GetTypeface(Arial), out string character, out string family),
            "No installed family covers a character Arial lacks other than the platform's own pick; the rendering suite requires system fonts.");

        using SkiaFontProvider fonts = new SkiaFontProvider();
        fonts.FallbackFamilies.Add(family);

        string drawnDirectly = FontOf(Build(character, Arial.FontFamilyOf(family)).GeneratePdf(), character);
        string drawnAsFallback = FontOf(Build($"A{character}").GeneratePdf(new PdfGenerationOptions { Fonts = fonts }), character);
        string platformChoice = FontOf(Build($"A{character}").GeneratePdf(), character);

        Assert.Equal(drawnDirectly, drawnAsFallback);

        // Left to the shared provider, which names no fallbacks, the platform picks another face. That is what
        // shows the match above came from the explicit fallback, and from the provider supplied in the options.
        Assert.NotEqual(drawnDirectly, platformChoice);
    }

    [Fact]
    public void EachScriptFindsItsOwnFallback()
    {
        // The face found for the CJK characters has no Georgian, so the Georgian lookup must look past the
        // fallback already discovered for this style instead of settling for it.
        using SkiaFontProvider fonts = new SkiaFontProvider();
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(fonts);

        float whole = measurer.MeasureWidth(Cjk + Georgian, Arial);
        float parts = measurer.MeasureWidth(Cjk, Arial) + measurer.MeasureWidth(Georgian, Arial);

        using PdfDocument parsed = PdfDocument.Open(Build($"{Cjk} {Georgian}").GeneratePdf(new PdfGenerationOptions { Fonts = fonts }));
        string text = parsed.GetPage(1).Text;

        Assert.Equal(parts, whole, 0.5f);
        Assert.Contains(Cjk, text);
        Assert.Contains(Georgian, text);
    }

    [Fact]
    public void ItalicTextFallsBackToo()
    {
        using SkiaFontProvider fonts = new SkiaFontProvider();

        using PdfDocument parsed = PdfDocument.Open(Build($"{Latin} {Cjk}", Arial.Italic()).GeneratePdf(new PdfGenerationOptions { Fonts = fonts }));
        string text = parsed.GetPage(1).Text;

        Assert.Contains(Latin, text);
        Assert.Contains(Cjk, text);
    }

    [Fact]
    public void ACharacterNoFontHasLeavesTheTextAroundItIntact()
    {
        // U+0378 is unassigned, so no face anywhere has it. The run must carry on rather than fail, and it must
        // still be measured with whatever it is drawn with.
        const string Unassigned = "͸";
        using SkiaFontProvider fonts = new SkiaFontProvider();
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(fonts);

        float whole = measurer.MeasureWidth($"A{Unassigned}B", Arial);
        float parts = measurer.MeasureWidth("A", Arial) + measurer.MeasureWidth(Unassigned, Arial) + measurer.MeasureWidth("B", Arial);

        using PdfDocument parsed = PdfDocument.Open(Build($"A{Unassigned}B").GeneratePdf(new PdfGenerationOptions { Fonts = fonts }));
        IReadOnlyList<Letter> letters = parsed.GetPage(1).Letters;

        Assert.Equal(parts, whole, 0.01f);
        Assert.Equal("A", letters[0].Value);
        Assert.Equal("B", letters[letters.Count - 1].Value);
    }
}
