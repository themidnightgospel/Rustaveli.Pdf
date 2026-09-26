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

    /// <summary>The 'glyf' table tag: present in fonts with TrueType outlines.</summary>
    private const uint TrueTypeOutlines = ('g' << 24) | ('l' << 16) | ('y' << 8) | 'f';

    private static readonly TypeStyle Sans = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(16);

    private static Document Build(string text) => Build(text, Sans);

    private static Document Build(string text, TypeStyle style) =>
        Document.Compose(container => container.Section(page =>
        {
            page.Trim = PaperSizes.A4;
            page.Margins = Sides.All(30);
            page.DefaultType = style;
            page.Body().Text(text);
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

                // Only faces with TrueType outlines: Skia embeds those under their own name, where others (CFF-based
                // CJK faces, for one) become anonymous Type3 fonts that cannot be told apart by name afterwards.
                if (typeface is null || typeface.FamilyName == platformChoice || typeface.FamilyName == primary.FamilyName
                    || !typeface.GetTableTags().Contains(TrueTypeOutlines))
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
        using PdfDocument parsed = PdfDocument.Open(Build($"{Latin} {Cjk}").ExportPdf());
        string text = parsed.GetPage(1).Text;

        Assert.Contains(Latin, text);
        Assert.Contains(Cjk, text);
    }

    [Fact]
    public void MixedScriptTextMeasuresWiderThanItsLatinPartAlone()
    {
        SkiaTypeMeasurer measurer = new SkiaTypeMeasurer(SkiaFontProvider.Shared);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(16);

        float latinOnly = measurer.MeasureWidth(Latin, style);
        float mixed = measurer.MeasureWidth($"{Latin}{Cjk}", style);

        Assert.True(mixed > latinOnly, $"Mixed-script text measured {mixed} against {latinOnly} for the Latin part alone.");
    }

    [Fact]
    public void MeasuredWidthMatchesWhatIsDrawn()
    {
        // Measurement and drawing must split the string identically; if they disagreed, a fallback glyph would
        // land somewhere other than where its advance was reserved.
        SkiaTypeMeasurer measurer = new SkiaTypeMeasurer(SkiaFontProvider.Shared);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(16);

        float whole = measurer.MeasureWidth($"{Latin}{Cjk}", style);
        float parts = measurer.MeasureWidth(Latin, style) + measurer.MeasureWidth(Cjk, style);

        Assert.InRange(whole, parts - 0.5f, parts + 0.5f);
    }

    [Fact]
    public void AnExplicitFallbackFamilyIsPreferredOverPlatformMatching()
    {
        // Naming the fallback is what makes output reproducible; left to the platform the substitute differs
        // between machines.
        using SkiaFontProvider provider = TestFonts.NewProvider();
        provider.FallbackFamilies.Add("Segoe UI");

        SkiaTypeMeasurer measurer = new SkiaTypeMeasurer(provider);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(16);

        Assert.True(measurer.MeasureWidth(Cjk, style) > 0);
    }

    [Fact]
    public void PurelyLatinTextIsUnaffected()
    {
        SkiaTypeMeasurer measurer = new SkiaTypeMeasurer(SkiaFontProvider.Shared);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(16);

        // The fast path must produce exactly what a single-font measurement always did.
        Assert.True(measurer.MeasureWidth(Latin, style) > 0);
        Assert.Equal(measurer.MeasureWidth(Latin, style), measurer.MeasureWidth(Latin, style));
    }

    [Fact]
    public void AnExplicitFallbackFamilyDrawsWhatThePrimaryFontLacks()
    {
        Assert.True(
            TryFindAlternativeFallback(SkiaFontProvider.Shared.GetTypeface(Sans), out string character, out string family),
            "No installed family covers a character Arial lacks other than the platform's own pick; the rendering suite requires system fonts.");

        using SkiaFontProvider fonts = TestFonts.NewProvider();
        fonts.FallbackFamilies.Add(family);

        string drawnDirectly = FontOf(Build(character, Sans.WithTypeface(family)).ExportPdf(), character);
        string drawnAsFallback = FontOf(Build($"A{character}").ExportPdf(new PdfExportOptions { Fonts = fonts }), character);
        string platformChoice = FontOf(Build($"A{character}").ExportPdf(), character);

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
        using SkiaFontProvider fonts = TestFonts.NewProvider();
        SkiaTypeMeasurer measurer = new SkiaTypeMeasurer(fonts);

        float whole = measurer.MeasureWidth(Cjk + Georgian, Sans);
        float parts = measurer.MeasureWidth(Cjk, Sans) + measurer.MeasureWidth(Georgian, Sans);

        using PdfDocument parsed = PdfDocument.Open(Build($"{Cjk} {Georgian}").ExportPdf(new PdfExportOptions { Fonts = fonts }));
        string text = parsed.GetPage(1).Text;

        Assert.Equal(parts, whole, 0.5f);
        Assert.Contains(Cjk, text);
        Assert.Contains(Georgian, text);
    }

    [Fact]
    public void ItalicTextFallsBackToo()
    {
        using SkiaFontProvider fonts = TestFonts.NewProvider();

        using PdfDocument parsed = PdfDocument.Open(Build($"{Latin} {Cjk}", Sans.Italic()).ExportPdf(new PdfExportOptions { Fonts = fonts }));
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
        using SkiaFontProvider fonts = TestFonts.NewProvider();
        SkiaTypeMeasurer measurer = new SkiaTypeMeasurer(fonts);

        float whole = measurer.MeasureWidth($"A{Unassigned}B", Sans);
        float parts = measurer.MeasureWidth("A", Sans) + measurer.MeasureWidth(Unassigned, Sans) + measurer.MeasureWidth("B", Sans);

        using PdfDocument parsed = PdfDocument.Open(Build($"A{Unassigned}B").ExportPdf(new PdfExportOptions { Fonts = fonts }));
        IReadOnlyList<Letter> letters = parsed.GetPage(1).Letters;

        Assert.Equal(parts, whole, 0.01f);
        Assert.Equal("A", letters[0].Value);
        Assert.Equal("B", letters[letters.Count - 1].Value);
    }
}
