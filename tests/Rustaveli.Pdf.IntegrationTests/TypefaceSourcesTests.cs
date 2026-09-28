using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.IntegrationTests.Fonts;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Where a <see cref="TypefaceLibrary"/> gets typefaces from: files, bytes, streams and embedded resources, under
/// their own names or one given, and folders searched as installed fonts are.
/// </summary>
public class TypefaceSourcesTests
{
    private const string Georgian = "NotoSansGeorgian-Regular.ttf";

    private static TypefaceLibrary Empty() => new TypefaceLibrary(includeInstalled: false);

    private static string PostScriptNameFor(TypefaceLibrary library, TypeStyle style) =>
        library.Shaper.Resolve(style).Names.PostScriptName;

    /// <summary>The face each character of <paramref name="text"/> is set in, by PostScript name.</summary>
    private static List<string> FacesSetting(TypefaceLibrary library, string text, TypeStyle style)
    {
        List<string> faces = [];

        foreach (ShapedGlyph glyph in library.Shaper.Walk(text.AsSpan(), style))
            faces.Add(glyph.Face.Names.PostScriptName);

        return faces;
    }

    [Fact]
    public void AFontRegisteredUnderANameIsFoundByIt()
    {
        TypefaceLibrary library = Empty();
        library.RegisterFile(FontAssets.PathOf(Georgian), typeface: "Brand");

        Assert.Equal("NotoSansGeorgian-Regular", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("Brand")));
        Assert.Equal("NotoSansGeorgian-Regular", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("Noto Sans Georgian")));
    }

    [Fact]
    public void FacesRegisteredUnderOneNameKeepTheirOwnStyles()
    {
        TypefaceLibrary library = Empty();
        library.RegisterFile(FontAssets.PathOf("NotoSans-Regular.ttf"), "Body");
        library.RegisterFile(FontAssets.PathOf("NotoSans-Bold.ttf"), "Body");
        library.RegisterFile(FontAssets.PathOf("NotoSans-Italic.ttf"), "Body");

        TypeStyle body = TypeStyle.Default.WithTypeface("Body");

        Assert.Equal("NotoSans-Regular", PostScriptNameFor(library, body));
        Assert.Equal("NotoSans-Bold", PostScriptNameFor(library, body.Bold()));
        Assert.Equal("NotoSans-Italic", PostScriptNameFor(library, body.Italic()));
    }

    [Fact]
    public void BytesAndStreamsCanBeRegisteredUnderANameToo()
    {
        TypefaceLibrary library = Empty();
        library.Register(File.ReadAllBytes(FontAssets.PathOf(Georgian)), "From Bytes");

        using (FileStream stream = File.OpenRead(FontAssets.PathOf("NotoSans-Regular.ttf")))
            library.Register(stream, "From A Stream");

        Assert.Equal("NotoSansGeorgian-Regular", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("From Bytes")));
        Assert.Equal("NotoSans-Regular", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("From A Stream")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankNameRegistersTheFontUnderItsOwnNamesOnly(string? name)
    {
        FontCatalog catalog = FontCatalog.WithoutSystemFonts();
        catalog.RegisterFile(FontAssets.PathOf(Georgian), name);

        Assert.Null(Assert.Single(catalog.RegisteredFaces).Alias);
    }

    [Fact]
    public void AFontEmbeddedInAnAssemblyCanBeRegistered()
    {
        TypefaceLibrary library = Empty();
        library.RegisterResource(typeof(TypefaceSourcesTests).Assembly, "Fonts.Georgian.ttf", "Embedded");

        Assert.Equal("NotoSansGeorgian-Regular", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("Embedded")));
    }

    [Fact]
    public void AResourceTheAssemblyDoesNotEmbedIsReportedWithThoseItDoes()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Empty().RegisterResource(typeof(TypefaceSourcesTests).Assembly, "Fonts.Missing.ttf"));

        Assert.Equal("resource", exception.ParamName);
        Assert.Contains("Fonts.Missing.ttf", exception.Message);
        Assert.Contains("Fonts.Georgian.ttf", exception.Message);
    }

    [Fact]
    public void ARegisteredResourceNeedsAnAssemblyAndAName()
    {
        Assert.Equal("assembly", Assert.Throws<ArgumentNullException>(() => Empty().RegisterResource(null!, "x")).ParamName);
        Assert.Equal(
            "resource",
            Assert.Throws<ArgumentNullException>(() => Empty().RegisterResource(typeof(TypefaceSourcesTests).Assembly, null!)).ParamName);
    }

    [Fact]
    public void AFolderIsSearchedForTypefaces()
    {
        using TemporaryFolder folder = TemporaryFolder.With(Georgian);
        TypefaceLibrary library = Empty();

        library.SearchFolder(folder.Path);

        Assert.Equal("NotoSansGeorgian-Regular", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("Noto Sans Georgian")));
    }

    [Fact]
    public void AFolderSuppliesFallbacksForCharactersOtherFacesLack()
    {
        using TemporaryFolder folder = TemporaryFolder.With(Georgian);
        TypefaceLibrary library = Empty();
        library.RegisterFile(FontAssets.PathOf("NotoSans-Regular.ttf"));
        library.SearchFolder(folder.Path);

        List<string> faces = FacesSetting(library, "aა", TypeStyle.Default.WithTypeface("Noto Sans"));

        Assert.Equal(["NotoSans-Regular", "NotoSansGeorgian-Regular"], faces);
    }

    [Fact]
    public void RegisteredTypefacesComeBeforeAFoldersOnes()
    {
        using TemporaryFolder folder = TemporaryFolder.With("NotoSans-Regular.ttf");
        TypefaceLibrary library = Empty();
        library.RegisterFile(FontAssets.PathOf("SpecimenSans.ttc"), "Noto Sans");
        library.SearchFolder(folder.Path);

        Assert.StartsWith("SpecimenSans", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("Noto Sans")));
    }

    [Fact]
    public void AMissingFolderAddsNothing()
    {
        TypefaceLibrary library = Empty();

        library.SearchFolder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"no-fonts-{Guid.NewGuid():N}"));

        Assert.Equal("NotoSans-Regular", PostScriptNameFor(library, TypeStyle.Default.WithTypeface("Noto Sans Georgian")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AFolderMustBeNamed(string? path) =>
        Assert.ThrowsAny<ArgumentException>(() => Empty().SearchFolder(path!));

    /// <summary>A folder of committed fonts, deleted afterwards.</summary>
    private sealed class TemporaryFolder : IDisposable
    {
        private TemporaryFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rustaveli-fonts-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public static TemporaryFolder With(params string[] fonts)
        {
            TemporaryFolder folder = new TemporaryFolder();

            foreach (string font in fonts)
                File.Copy(FontAssets.PathOf(font), System.IO.Path.Combine(folder.Path, font));

            return folder;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

