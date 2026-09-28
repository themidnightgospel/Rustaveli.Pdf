using System.Runtime.InteropServices;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Documents exported as XPS, which only Windows writes.
/// </summary>
public class XpsExportTests
{
    private static Document TwoPages() => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = new Extent(200, 100);
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
        section.Body().Stack(stack =>
        {
            stack.Add().Text("First");
            stack.Add().NewPage();
            stack.Add().Text("Second");
        });
    }));

    [Fact]
    public void ADocumentIsOneXpsPackageOfItsPages()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Throws<PlatformNotSupportedException>(() => TwoPages().ExportXps());
            return;
        }

        // A ZIP package, whose part names are written as they are: a document sequence, and a part for each page.
        byte[] package = TwoPages().ExportXps();
        string names = System.Text.Encoding.ASCII.GetString(package);

        Assert.Equal("PK", names.Substring(0, 2));
        Assert.Contains(".fdseq", names, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.fpage", names, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2.fpage", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("3.fpage", names, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADocumentIsWrittenWhereTheCallerSays()
    {
        string path = Path.Combine(Path.GetTempPath(), $"document-{Guid.NewGuid():N}.xps");

        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Throws<PlatformNotSupportedException>(() => TwoPages().ExportXps(path));
                return;
            }

            TwoPages().ExportXps(path);

            Assert.Equal((byte)'P', File.ReadAllBytes(path)[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingGlyphsCanBeRefused()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        Document document = Document.Compose(composition => composition.Section(section =>
            section.Body().Text(text => text.Run("").Typeface(TestFonts.Sans))));

        Assert.Throws<MissingGlyphException>(() => document.ExportXps(new VectorExportOptions { RequireEveryGlyph = true, Typefaces = TestFonts.NewLibrary(includeInstalled: false) }));
    }

    [Fact]
    public void ADocumentAndAPathAreNeeded()
    {
        Assert.Throws<ArgumentNullException>(() => XpsExport.ExportXps(null!));
        Assert.Throws<ArgumentNullException>(() => XpsExport.ExportXps(null!, "x.xps"));
        Assert.ThrowsAny<ArgumentException>(() => TwoPages().ExportXps(string.Empty));
    }
}
