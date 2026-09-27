namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// <see cref="PdfExportOptions.RequireEveryGlyph"/>, against a library of the committed Latin faces alone, which with
/// the bundled faces has no CJK.
/// </summary>
public class MissingGlyphTests
{
    private static readonly TypefaceLibrary Latin = TestFonts.NewLibrary(includeInstalled: false);

    private static Document Build(string text) => Document.Compose(composition => composition.Section(section =>
    {
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
        section.Body().Text(text);
    }));

    [Fact]
    public void ACharacterNoTypefaceHasFailsAnExportThatRequiresEveryGlyph()
    {
        MissingGlyphException exception = Assert.Throws<MissingGlyphException>(() =>
            Build("Hello 世界").ExportPdf(new PdfExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));

        Assert.Equal(["世", "界"], exception.Characters);
        Assert.StartsWith("No typeface has these characters: U+4E16 '世', U+754C '界'.", exception.Message);
        Assert.IsAssignableFrom<TypesettingException>(exception);
    }

    [Fact]
    public void WithoutTheRequirementTheCharacterIsDrawnAsABox()
    {
        byte[] pdf = Build("Hello 世界").ExportPdf(new PdfExportOptions { Typefaces = Latin });

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void TextEveryTypefaceCoversPasses()
    {
        byte[] pdf = Build("Hello, world").ExportPdf(new PdfExportOptions { Typefaces = Latin, RequireEveryGlyph = true });

        Assert.NotEmpty(pdf);
    }

    [Theory]
    [InlineData("​")]
    [InlineData("­")]
    [InlineData("⁠")]
    [InlineData("﻿")]
    public void CharactersThatAreNeverDrawnNeedNoGlyph(string invisible)
    {
        byte[] pdf = Build($"a{invisible}b").ExportPdf(new PdfExportOptions { Typefaces = Latin, RequireEveryGlyph = true });

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void ACharacterIsNamedOnceHoweverOftenItIsUsed()
    {
        MissingGlyphException exception = Assert.Throws<MissingGlyphException>(() =>
            Build("世 世 世").ExportPdf(new PdfExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));

        Assert.Equal(["世"], exception.Characters);
        Assert.StartsWith("No typeface has this character: U+4E16 '世'.", exception.Message);
    }

    [Fact]
    public void ACharacterBeyondTheBasicPlaneIsNamedWhole()
    {
        MissingGlyphException exception = Assert.Throws<MissingGlyphException>(() =>
            Build("\U0001F600").ExportPdf(new PdfExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));

        Assert.Equal(["\U0001F600"], exception.Characters);
        Assert.Contains("U+1F600", exception.Message);
    }

    [Fact]
    public void ALongListIsCutShortInTheMessage()
    {
        string many = new string(Enumerable.Range(0x4E00, 25).Select(codepoint => (char)codepoint).ToArray());

        MissingGlyphException exception = Assert.Throws<MissingGlyphException>(() =>
            Build(many).ExportPdf(new PdfExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));

        Assert.Equal(25, exception.Characters.Count);
        Assert.Contains("U+4E13", exception.Message);
        Assert.DoesNotContain("U+4E14", exception.Message);
        Assert.Contains(" and 5 more.", exception.Message);
    }

    [Fact]
    public void PageImagesRequireEveryGlyphToo()
    {
        MissingGlyphException exception = Assert.Throws<MissingGlyphException>(() =>
            Build("世").ExportImages(new ImageExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));

        Assert.Equal(["世"], exception.Characters);
        Assert.NotEmpty(Build("世").ExportImages(new ImageExportOptions { Typefaces = Latin, Resolution = 36 }));
    }

    [Fact]
    public void AFailedExportToAFileLeavesNoFileBehind()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf");

        Assert.Throws<MissingGlyphException>(() =>
            Build("世").ExportPdf(path, new PdfExportOptions { Typefaces = Latin, RequireEveryGlyph = true }));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*"));
    }
}
