using System.Text;
using System.Text.RegularExpressions;
using Rustaveli.Pdf.Fonts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// How inks and faces come out in the file: process colours as themselves, spot inks as separations, opacity as a
/// graphics state, and every kind of face embedded so its text reads back.
/// </summary>
public class InkAndFontOutputTests
{
    private static readonly Ink Process = Ink.Cmyk(0.1f, 0.2f, 0.3f, 0.4f);
    private static readonly Ink Pantone = Ink.Spot("PANTONE 186 C", Ink.Cmyk(0, 1, 0.8f, 0.05f));
    private static readonly Ink Signal = Ink.Spot("Signal Red", Ink.Rgb(220, 30, 30));

    private static string FontPath(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "fonts", name);

    private static string Export(Action<StackComposer> compose, TypefaceLibrary? typefaces = null) =>
        Encoding.Latin1.GetString(Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 400);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Stack(compose);
        })).ExportPdf(new PdfExportOptions { Compress = false, Typefaces = typefaces }));

    [Fact]
    public void ProcessInksAreWrittenAsCmykForFillsAndStrokes()
    {
        // Rules are filled; an underline is stroked.
        string pdf = Export(stack =>
        {
            stack.Add().Height(20).Fill(Process);
            stack.Add().Text(text => text.Run("Underlined").Underline().Ink(Process));
        });

        Assert.Matches(@"0\.1 0\.2 0\.3 0\.4 k", pdf);
        Assert.Matches(@"0\.1 0\.2 0\.3 0\.4 K", pdf);
    }

    [Fact]
    public void ASpotInkIsASeparationWithItsFallbackWrittenOnce()
    {
        string pdf = Export(stack =>
        {
            stack.Add().Height(20).Fill(Pantone);
            stack.Add().Height(20).Fill(Pantone.Tint(0.5f));
            stack.Add().Text(text => text.Run("Spot underline").Underline().Ink(Pantone));
            stack.Add().Height(20).Fill(Signal);
        });

        Assert.Single(Regex.Matches(pdf, @"/Separation\s*/PANTONE#20186#20C\s*/DeviceCMYK"));
        Assert.Single(Regex.Matches(pdf, @"/Separation\s*/Signal#20Red\s*/DeviceRGB"));
        Assert.Matches(@"/C1\s*\[0 1 0\.8 0\.05\]", pdf);
        Assert.Matches(@"/C0\s*\[1 1 1\]", pdf);
        Assert.Matches(@"/CS\d+ cs\s+1 sc", pdf);
        Assert.Matches(@"\s0\.5 sc\s", pdf);
        Assert.Matches(@"/CS\d+ CS\s+1 SC", pdf);
    }

    [Fact]
    public void OpacityIsAGraphicsState()
    {
        string pdf = Export(stack =>
        {
            stack.Add().Height(20).Fill(Ink.Rgb(10, 20, 30).WithOpacity(0.5f));
            stack.Add().Text(text => text.Run("Faint").Underline().Ink(Ink.Rgb(10, 20, 30).WithOpacity(0.25f)));
        });

        Assert.Matches(@"/ca 0\.5", pdf);
        Assert.Matches(@"/CA 0\.25", pdf);
        Assert.Matches(@"/GS\d+ gs", pdf);
    }

    [Fact]
    public void RoundedStrokesAreStrokedInTheirInk()
    {
        string pdf = Export(stack => stack.Add().Height(40).Stroke(2).StrokeInk(Process).RoundCorners(6).Text("Framed"));

        Assert.Matches(@"0\.1 0\.2 0\.3 0\.4 K", pdf);
        Assert.Matches(@"\sc\s", pdf);
    }

    [Fact]
    public void ACffFaceIsEmbeddedAsASubsetAndReadsBack()
    {
        string path = FontPath("SpecimenCff-Regular.otf");
        OpenTypeFont face = OpenTypeFont.LoadFile(path);
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        library.RegisterFile(path);

        byte[] bytes = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 200);
            section.DefaultType = TypeStyle.Default.WithTypeface(face.Names.PreferredFamily).WithPointSize(14);
            section.Body().Text("Compact fonts read back");
        })).ExportPdf(new PdfExportOptions { Typefaces = library, Compress = false });

        using PdfDocument pdf = PdfDocument.Open(bytes);
        string raw = Encoding.Latin1.GetString(bytes);

        Assert.Equal("Compact fonts read back", pdf.GetPage(1).Text);
        Assert.Matches(@"/Subtype\s*/CIDFontType0\b", raw);
        Assert.Matches(@"/FontFile3", raw);
        Assert.Matches(@"/Subtype\s*/CIDFontType0C", raw);
        Assert.DoesNotMatch(@"/Subtype\s*/OpenType", raw);
        Assert.Matches(@"/FontName\s*/[A-Z]{6}\+SpecimenCff-Regular", raw);
    }

    [Fact]
    public void EveryFaceOfACollectionCanSetText()
    {
        string path = FontPath("SpecimenSans.ttc");
        IReadOnlyList<OpenTypeFont> faces = OpenTypeFont.LoadAll(File.ReadAllBytes(path));
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        library.RegisterFile(path);

        byte[] bytes = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 300);
            section.Body().Stack(stack =>
            {
                foreach (OpenTypeFont face in faces)
                {
                    TypeStyle style = TypeStyle.Default.WithTypeface(face.Names.PreferredFamily)
                        .WithWeight((TypeWeight)face.Style.Weight)
                        .Italic(face.Style.Slant != FontSlant.Upright);
                    stack.Add().Text(text => text.Run(face.Names.FullName).Style(_ => style));
                }
            });
        })).ExportPdf(new PdfExportOptions { Typefaces = library });

        using PdfDocument pdf = PdfDocument.Open(bytes);
        string text = pdf.GetPage(1).Text;
        HashSet<string> fonts = [.. pdf.GetPage(1).Letters.Select(letter => letter.FontName!)];

        foreach (OpenTypeFont face in faces)
            Assert.Contains(face.Names.FullName.Replace(" ", string.Empty), text.Replace(" ", string.Empty));

        Assert.Equal(faces.Count, fonts.Count);
    }

    [Fact]
    public void TextKeepsItsCharactersWhenItNeedsTwoFaces()
    {
        // A supplementary-plane character from a fallback face still maps back to its one character.
        using PdfDocument pdf = PdfDocument.Open(Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 200);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(14);
            section.Body().Text("A\U0001D400B");
        })).ExportPdf());

        IReadOnlyList<Letter> letters = pdf.GetPage(1).Letters;
        Assert.Equal("A\U0001D400B", string.Concat(letters.Select(letter => letter.Value)));
    }
}
