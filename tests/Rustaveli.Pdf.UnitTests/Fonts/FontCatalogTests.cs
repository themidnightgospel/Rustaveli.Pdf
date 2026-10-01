using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// Finding fonts, with the committed font folder standing in for the machine's installed fonts so every result is
/// the same on every host.
/// </summary>
public class FontCatalogTests
{
    private static readonly SystemFontIndex Committed = new SystemFontIndex([TestFonts.Directory]);

    private static FontCatalog Catalog() => new FontCatalog(Committed);

    private static string? PostScriptName(FontCatalog catalog, FontRequest request) =>
        catalog.FindFace(request)?.Names.PostScriptName;

    [Fact]
    public void DescribesEveryInstalledFaceWithoutLoadingIt()
    {
        SystemFontIndex index = new SystemFontIndex([TestFonts.Directory]);

        Assert.Equal(
            new[]
            {
                "NotoSans-Bold", "NotoSans-BoldItalic", "NotoSans-Italic", "NotoSans-Regular", "NotoSansArabic-Regular",
                "NotoSansDevanagari-Regular", "NotoSansGeorgian-Regular",
                "SpecimenCff-Regular", "SpecimenCjk-Regular", "SpecimenLayout-Regular", "SpecimenSans-Regular",
                "SpecimenSans-SemiBold", "SpecimenSans-Italic", "SpecimenSubrs-Regular",

                // The corpus folder inside it, read after the folder's own files: a kern-table font, a collection, and
                // the CFF2 and TrueType builds of a variable font.
                "KernTableTest-Regular", "NotoSans-Regular", "NotoSans-Bold", "SourceSans3VF-ExtraLight", "SourceSans3VF-ExtraLight"
            },
            index.Faces.Select(face => face.Names.PostScriptName));
        Assert.All(index.Faces, face => Assert.False(face.IsLoaded));
        Assert.Equal(
            new[]
            {
                "Kern Table Test", "Noto Sans", "Noto Sans Arabic", "Noto Sans Devanagari", "Noto Sans Georgian", "SourceSans3VF",
                "Specimen Cff", "Specimen Cjk", "Specimen Layout", "Specimen Sans", "Specimen Subrs"
            },
            index.Families.Families);
        Assert.Equal(new[] { TestFonts.Directory }, index.Directories);
    }

    [Fact]
    public void ReadsTheStyleAndOutlinesOfEachFace()
    {
        FontFaceInfo semiBold = Committed.Faces.Single(face => face.Names.PostScriptName == "SpecimenSans-SemiBold");
        FontFaceInfo cff = Committed.Faces.Single(face => face.Names.PostScriptName == "SpecimenCff-Regular");

        Assert.Equal(new FaceStyle(600, 5, FontSlant.Upright), semiBold.Style);
        Assert.Equal(1, semiBold.FaceIndex);
        Assert.Equal(TestFonts.PathOf(TestFonts.CollectionFile), semiBold.FilePath);
        Assert.Equal(OutlineFormat.Cff, cff.Outlines);
        Assert.True(cff.IsEmbeddable);
        Assert.False(cff.IsRegistered);
        Assert.Contains("SpecimenCff-Regular.otf", cff.ToString());
    }

    [Theory]
    [InlineData("Noto Sans", 400, 0, "NotoSans-Regular")]
    [InlineData("noto sans", 700, 0, "NotoSans-Bold")]
    [InlineData("  Noto Sans ", 900, 0, "NotoSans-Bold")]
    [InlineData("Noto Sans", 300, 0, "NotoSans-Regular")]
    [InlineData("Noto Sans", 400, 1, "NotoSans-Italic")]
    [InlineData("Noto Sans", 700, 1, "NotoSans-BoldItalic")]
    [InlineData("Noto Sans", 600, 2, "NotoSans-BoldItalic")]
    [InlineData("Noto Sans", 400, 2, "NotoSans-Italic")]
    [InlineData("Specimen Sans", 600, 0, "SpecimenSans-SemiBold")]
    [InlineData("Specimen Sans", 700, 0, "SpecimenSans-SemiBold")]
    [InlineData("Specimen Sans", 400, 0, "SpecimenSans-Regular")]
    [InlineData("Specimen Sans", 400, 1, "SpecimenSans-Italic")]
    [InlineData("Specimen Sans SemiBold", 400, 0, "SpecimenSans-SemiBold")]
    [InlineData("Noto Sans Bold", 400, 0, "NotoSans-Bold")]
    [InlineData("NotoSans-Italic", 400, 0, "NotoSans-Italic")]
    public void MatchesFamiliesAndStyles(string family, int weight, int slant, string expected)
    {
        FontRequest request = new FontRequest(family, weight, (FontSlant)slant);

        Assert.Equal(expected, PostScriptName(Catalog(), request));
    }

    [Fact]
    public void FindsNothingForAnUnknownFamily()
    {
        FontCatalog catalog = Catalog();

        Assert.Null(catalog.FindFace(new FontRequest("Comic Sans MS")));
        Assert.Null(catalog.Match(new FontRequest("Comic Sans MS")));
    }

    [Fact]
    public void LoadsAMatchOnceAndKeepsIt()
    {
        FontCatalog catalog = new FontCatalog(new SystemFontIndex([TestFonts.Directory]));
        FontFaceInfo face = catalog.FindFace(new FontRequest("Noto Sans Georgian"))!;

        Assert.False(face.IsLoaded);

        OpenTypeFont font = catalog.Match(new FontRequest("NOTO SANS GEORGIAN"))!;

        Assert.True(face.IsLoaded);
        Assert.Same(font, catalog.Match(new FontRequest("Noto Sans Georgian")));
        Assert.Equal(225, font.GlyphCount);
    }

    [Fact]
    public void LetsARegisteredFamilyShadowAnInstalledOne()
    {
        FontCatalog catalog = Catalog();
        IReadOnlyList<FontFaceInfo> registered = catalog.Register(SyntheticFont.Named("Noto Sans").Build());

        FontFaceInfo face = catalog.FindFace(new FontRequest("Noto Sans", 700))!;

        Assert.Same(registered[0], face);
        Assert.True(face.IsRegistered);
        Assert.True(face.IsLoaded);
        Assert.Null(face.FilePath);
        Assert.Equal("NotoSans-Bold", PostScriptName(catalog, new FontRequest("NotoSans-Bold")));
    }

    [Fact]
    public void ForgetsEarlierMatchesWhenAFontIsRegistered()
    {
        FontCatalog catalog = Catalog();
        Assert.Equal("NotoSans-Regular", PostScriptName(catalog, new FontRequest("Noto Sans")));

        catalog.Register(SyntheticFont.Named("Noto Sans").Build());

        Assert.True(catalog.FindFace(new FontRequest("Noto Sans"))!.IsRegistered);
    }

    [Fact]
    public void RegistersEveryFaceOfACollectionFromAStream()
    {
        FontCatalog catalog = FontCatalog.WithoutSystemFonts();
        using FileStream stream = File.OpenRead(TestFonts.PathOf(TestFonts.CollectionFile));

        IReadOnlyList<FontFaceInfo> faces = catalog.Register(stream);

        Assert.Equal(3, faces.Count);
        Assert.Equal(faces, catalog.RegisteredFaces);
        Assert.Empty(catalog.System.Faces);
        Assert.Equal("SpecimenSans-SemiBold", PostScriptName(catalog, new FontRequest("Specimen Sans", 600)));
    }

    [Fact]
    public void RegistersAFontFile()
    {
        FontCatalog catalog = FontCatalog.WithoutSystemFonts();

        FontFaceInfo face = catalog.RegisterFile(TestFonts.PathOf(TestFonts.GeorgianFile)).Single();

        Assert.Equal(TestFonts.PathOf(TestFonts.GeorgianFile), face.FilePath);
        Assert.Same(face, catalog.FindFace(new FontRequest("Noto Sans Georgian")));
        Assert.True(face.Covers('\u10D0'));
    }

    [Fact]
    public void RejectsRegisteringSomethingThatIsNotAFont()
    {
        FontCatalog catalog = FontCatalog.WithoutSystemFonts();

        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

        Assert.Throws<FontFormatException>(() => catalog.Register(data));
        Assert.Empty(catalog.RegisteredFaces);
    }

    [Fact]
    public void FallsBackToAnInstalledFaceThatHasTheCharacter()
    {
        FontCatalog catalog = new FontCatalog(new SystemFontIndex([TestFonts.Directory]));

        FontFaceInfo? georgian = catalog.FindFallbackFace('\u10D0', new FontRequest("Noto Sans"));

        Assert.Equal("NotoSansGeorgian-Regular", georgian?.Names.PostScriptName);

        // Coverage came from the character map alone; nothing was loaded to answer.
        Assert.False(georgian!.IsLoaded);

        // The next letter of the same block is found in the same face.
        Assert.Same(georgian, catalog.FindFallbackFace('\u10D5', new FontRequest("Noto Sans")));
        Assert.Equal(225, catalog.FindFallback('\u10D5', new FontRequest("Noto Sans"))!.GlyphCount);
    }

    [Fact]
    public void FallsBackToTheClosestStyleFirst()
    {
        FontCatalog catalog = Catalog();

        FontFaceInfo? bold = catalog.FindFallbackFace('Q', new FontRequest("Anything", 700));
        FontFaceInfo? italic = catalog.FindFallbackFace('Q', new FontRequest("Anything", 400, FontSlant.Italic));

        Assert.Equal(new FaceStyle(700, 5, FontSlant.Upright), bold?.Style);
        Assert.Equal(FontSlant.Italic, italic?.Style.Slant);
    }

    [Fact]
    public void TriesTheFallbackFamiliesInOrderFirst()
    {
        FontCatalog catalog = Catalog();
        string[] fallbacks = ["No Such Family", "Noto Sans Georgian", "Specimen Sans"];

        FontFaceInfo? latin = catalog.FindFallbackFace('A', new FontRequest("x"), fallbacks);
        FontFaceInfo? georgian = catalog.FindFallbackFace('\u10D0', new FontRequest("x"), fallbacks);

        Assert.Equal("SpecimenSans-Regular", latin?.Names.PostScriptName);
        Assert.Equal("NotoSansGeorgian-Regular", georgian?.Names.PostScriptName);
    }

    [Fact]
    public void TriesRegisteredFacesBeforeInstalledOnes()
    {
        FontCatalog catalog = Catalog();
        catalog.RegisterFile(TestFonts.PathOf(TestFonts.GeorgianFile));

        FontFaceInfo? face = catalog.FindFallbackFace('\u10D0', new FontRequest("Noto Sans"));

        Assert.True(face?.IsRegistered);
    }

    [Fact]
    public void FindsNoFallbackForACharacterNoFontHas()
    {
        FontCatalog catalog = Catalog();

        // A Hangul syllable: the committed fonts have Latin, Georgian, Arabic, Devanagari and some Chinese, but no Korean.
        Assert.Null(catalog.FindFallbackFace(0xAC00, new FontRequest("Noto Sans")));

        // Remembered, and answered the same way again.
        Assert.Null(catalog.FindFallback(0xAC00, new FontRequest("Noto Sans")));
    }

    [Fact]
    public void NeverFallsBackToAFaceItCannotEmbed()
    {
        FontCatalog catalog = FontCatalog.WithoutSystemFonts();
        catalog.Register(SyntheticFont.Named("Bitmap").Without("glyf").Without("loca").Build());

        Assert.Equal(OutlineFormat.None, catalog.RegisteredFaces[0].Outlines);
        Assert.Null(catalog.FindFallbackFace('A', new FontRequest("x"), ["Bitmap"]));
        Assert.NotNull(catalog.FindFace(new FontRequest("Bitmap")));
    }

    [Fact]
    public void ChoosesAFaceItCanEmbedOverACloserOneOfTheSameFamilyItCannot()
    {
        // A variable font installed both as TrueType and as CFF2 lists one family twice, and only one can be embedded.
        FontCatalog catalog = FontCatalog.WithoutSystemFonts();
        catalog.Register(SyntheticFont.Named("Mixed").Without("glyf").Without("loca").Build());
        catalog.Register(SyntheticFont.Named("Mixed", weight: 700).Build());

        Assert.Equal(OutlineFormat.TrueType, catalog.FindFace(new FontRequest("Mixed"))!.Outlines);
    }

    [Fact]
    public void NeverSetsTextInAFaceItCannotEmbed()
    {
        // A face without outlines can be measured but not drawn into a PDF, so text asking for it is set in a face
        // that can be: with no substitute for its kind to be had, any registered one.
        TypefaceLibrary library = new TypefaceLibrary(FontCatalog.WithoutSystemFonts());
        library.Register(SyntheticFont.Named("Bitmap").Without("glyf").Without("loca").Build());
        library.Register(SyntheticFont.Named("Outlined").Build());

        Assert.Equal("Outlined", library.Shaper.Resolve(TypeStyle.Default.WithTypeface("Bitmap")).Names.Family);
        Assert.Equal("Outlined", library.Shaper.Resolve(TypeStyle.Default.WithTypeface("Outlined")).Names.Family);
    }

    [Fact]
    public void LooksBeyondTheFaceKnownForABlockWhenItLacksTheCharacter()
    {
        FontCatalog catalog = new FontCatalog(new SystemFontIndex([TestFonts.Directory]));
        FontRequest request = new FontRequest("Noto Sans");

        Assert.NotNull(catalog.FindFallbackFace('\u10D0', request));

        // U+1080, a Myanmar letter, shares the Georgian letters' block but no committed font has it.
        Assert.Null(catalog.FindFallbackFace('\u1080', request));
    }

    [Fact]
    public void ReadsCoverageFromTheFontOnceItsFileIsInMemory()
    {
        FontFaceInfo[] faces = new SystemFontIndex([TestFonts.Directory]).Faces
            .Where(face => face.FilePath == TestFonts.PathOf(TestFonts.CollectionFile))
            .ToArray();

        faces[0].Load();

        // The italic face shares the file, now in memory: its coverage comes from parsing it there.
        Assert.False(faces[2].IsLoaded);
        Assert.True(faces[2].Covers('A'));
        Assert.True(faces[2].IsLoaded);
    }

    [Fact]
    public void NamesARegisteredFaceByItsOrigin()
    {
        FontFaceInfo face = FontCatalog.WithoutSystemFonts().Register(SyntheticFont.Named("Custom").Build())[0];

        Assert.Equal("Custom Regular (registered#0)", face.ToString());
    }

    [Theory]
    [InlineData("Noto Sans", 400, 0, 5, true)]
    [InlineData(" noto SANS ", 400, 0, 5, true)]
    [InlineData("Noto Serif", 400, 0, 5, false)]
    [InlineData("Noto Sans", 700, 0, 5, false)]
    [InlineData("Noto Sans", 400, 1, 5, false)]
    [InlineData("Noto Sans", 400, 0, 3, false)]
    public void ComparesRequestsIgnoringCaseAndPadding(string family, int weight, int slant, int width, bool equal)
    {
        FontRequest reference = new FontRequest("Noto Sans");
        FontRequest other = new FontRequest(family, weight, (FontSlant)slant, width);
        FontCatalog.FamilyIgnoringCase comparer = FontCatalog.FamilyIgnoringCase.Instance;

        Assert.Equal(equal, comparer.Equals(reference, other));

        if (equal)
            Assert.Equal(comparer.GetHashCode(reference), comparer.GetHashCode(other));
    }

    [Fact]
    public void RejectsARequestWithoutAFamily()
    {
        Assert.Throws<ArgumentNullException>(() => Catalog().FindFace(new FontRequest(null!)));
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new FontCatalog(null!));
        Assert.Throws<ArgumentNullException>(() => FontCatalog.WithoutSystemFonts().Register((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => FontCatalog.WithoutSystemFonts().RegisterFile(null!));
    }

    [Fact]
    public void CanScanTheFontsInstalledOnThisMachine()
    {
        // Whatever is installed: the point is that real-world font folders scan without an exception escaping.
        FontCatalog catalog = new FontCatalog();

        Assert.Same(SystemFontIndex.Current, catalog.System);
        Assert.All(catalog.System.Faces, face => Assert.False(string.IsNullOrEmpty(face.Names.PostScriptName)));
        Assert.Equal(SystemFontDirectories.ForCurrentPlatform(), catalog.System.Directories);
    }
}
