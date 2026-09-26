using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Text;
using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Exercises supplying typefaces to a library rather than relying on what the host has installed.
/// </summary>
/// <remarks>
/// The font registered is the committed Noto Sans (tests/assets/fonts), so these behave identically on every host.
/// The libraries here see no installed typefaces unless a test says otherwise: registration is what is under test.
/// </remarks>
public class FontRegistrationTests
{
    private static string FontFile => TestFonts.PathOf("NotoSans-Regular.ttf");

    private static Document Build(string text, TypeStyle style) => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = PaperSizes.A4;
        section.Margins = Sides.All(30);
        section.DefaultType = style;
        section.Body().Text(text);
    }));

    [Fact]
    public void RegisteredFontIsUsedForMeasurement()
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        library.RegisterFile(FontFile);

        OpenTypeMeasurer measurer = new OpenTypeMeasurer(library.Shaper);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans);

        Assert.Equal(TestFonts.Sans, library.Shaper.Resolve(style).Names.PreferredFamily);
        Assert.True(measurer.GetMetrics(style).Ascent > 0, "A registered font must report a positive ascent.");
        Assert.True(measurer.MeasureWidth("Hello", style) > 0);
    }

    [Fact]
    public void RegistersFromBytesStreamsAndFiles()
    {
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans);

        TypefaceLibrary fromBytes = new TypefaceLibrary(includeInstalled: false);
        fromBytes.Register(File.ReadAllBytes(FontFile));

        TypefaceLibrary fromStream = new TypefaceLibrary(includeInstalled: false);
        using (FileStream stream = File.OpenRead(FontFile))
            fromStream.Register(stream);

        TypefaceLibrary fromFile = new TypefaceLibrary(includeInstalled: false);
        fromFile.RegisterFile(FontFile);

        Assert.Equal(TestFonts.Sans, fromBytes.Shaper.Resolve(style).Names.PreferredFamily);
        Assert.Equal(TestFonts.Sans, fromStream.Shaper.Resolve(style).Names.PreferredFamily);
        Assert.Equal(TestFonts.Sans, fromFile.Shaper.Resolve(style).Names.PreferredFamily);
    }

    [Fact]
    public void RejectsDataThatIsNotAFont()
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        using MemoryStream stream = new MemoryStream("this is definitely not a font"u8.ToArray());

        ArgumentException error = Assert.Throws<ArgumentException>(() => library.Register(stream));

        Assert.Equal("stream", error.ParamName);
        Assert.IsType<FontFormatException>(error.InnerException);
        Assert.Throws<ArgumentException>(() => library.Register("nor this"u8.ToArray()));
    }

    [Fact]
    public void RefusesMissingArguments()
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);

        Assert.Throws<ArgumentNullException>(() => library.Register((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => library.Register((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => library.RegisterFile(null!));
        Assert.Throws<ArgumentNullException>(() => library.Fallbacks = null!);
    }

    [Fact]
    public void DocumentsExportWithTheLibraryTheOptionsName()
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        library.RegisterFile(FontFile);

        byte[] bytes = Build("Rendered with a registered font", TypeStyle.Default.WithTypeface(TestFonts.Sans))
            .ExportPdf(new PdfExportOptions { Typefaces = library });

        using PdfDocument parsed = PdfDocument.Open(bytes);
        Assert.Contains("Rendered", parsed.GetPage(1).Text);
        Assert.EndsWith("NotoSans-Regular", parsed.GetPage(1).Letters[0].FontName);
    }

    [Fact]
    public void ResolvesEachStyleOnceAndDistinguishesWeights()
    {
        TypefaceLibrary library = TestFonts.NewLibrary(includeInstalled: false);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(14);

        // The layout engine measures the same styles many times per document, so resolution is cached.
        Assert.Same(library.Shaper.Resolve(style), library.Shaper.Resolve(style.WithPointSize(20)));
        Assert.NotSame(library.Shaper.Resolve(style), library.Shaper.Resolve(style.Bold()));
        Assert.NotSame(library.Shaper.Resolve(style), library.Shaper.Resolve(style.Italic()));
    }

    [Fact]
    public void ARegisteredFaceShadowsTheSubstitutesForAnUnknownTypeface()
    {
        // With nothing installed and nothing like it registered, any registered face sets the text rather than
        // the document being refused.
        TypefaceLibrary library = TestFonts.NewLibrary(includeInstalled: false);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(library.Shaper);

        TypeStyle style = TypeStyle.Default.WithTypeface("A Font That Certainly Does Not Exist");

        Assert.True(measurer.MeasureWidth("Hello", style) > 0);
        Assert.Equal(TestFonts.Sans, library.Shaper.Resolve(style).Names.PreferredFamily);
    }

    [Fact]
    public void AnUnknownTypefaceIsSubstitutedFromTheInstalledOnes()
    {
        TypefaceLibrary library = new TypefaceLibrary();
        TypeStyle style = TypeStyle.Default.WithTypeface("A Font That Certainly Does Not Exist");

        // Substitution, like a desktop publisher's, rather than failure: the text still measures.
        Assert.True(new OpenTypeMeasurer(library.Shaper).MeasureWidth("Hello", style) > 0);
    }

    [Fact]
    public void ALibraryWithNoTypefacesAtAllRefusesTheDocument()
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        Document document = Build("Nothing can set this", TypeStyle.Default);

        Exception error = Assert.ThrowsAny<Exception>(() => document.ExportPdf(new PdfExportOptions { Typefaces = library }));

        CompositionException cause = Assert.IsType<CompositionException>(error as CompositionException ?? error.InnerException);
        Assert.Contains("Helvetica", cause.Message);
        Assert.Contains("TypefaceLibrary.Register", cause.Message);
    }

    [Fact]
    public void RegisteringAgainTakesEffectForLaterDocuments()
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        TypeShaper before = library.Shaper;

        library.RegisterFile(FontFile);

        Assert.NotSame(before, library.Shaper);
        Assert.Equal(TestFonts.Sans, library.Shaper.Resolve(TypeStyle.Default.WithTypeface(TestFonts.Sans)).Names.PreferredFamily);
    }
}
