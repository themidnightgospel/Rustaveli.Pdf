using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>Finding font files on disk and describing them without loading them.</summary>
public class FontDiscoveryTests
{
    [Fact]
    public void ListsEachPlatformsFontFolders()
    {
        const string LocalAppData = @"C:\Users\me\AppData\Local";

        Assert.Equal(
            new[] { @"C:\Windows\Fonts", Path.Combine(LocalAppData, "Microsoft", "Windows", "Fonts") },
            SystemFontDirectories.For(FontPlatform.Windows, @"C:\Windows\Fonts", LocalAppData, "/home"));
        Assert.Equal(
            new[] { "/System/Library/Fonts", "/Library/Fonts", Path.Combine("/Users/me", "Library", "Fonts") },
            SystemFontDirectories.For(FontPlatform.MacOS, null, null, "/Users/me"));
        Assert.Equal(
            new[]
            {
                "/usr/share/fonts", "/usr/local/share/fonts",
                Path.Combine("/home/me", ".local", "share", "fonts"), Path.Combine("/home/me", ".fonts")
            },
            SystemFontDirectories.For(FontPlatform.Unix, null, null, "/home/me"));
    }

    [Fact]
    public void LeavesOutFoldersItHasNoPathFor()
    {
        Assert.Empty(SystemFontDirectories.For(FontPlatform.Windows, null, "", null));
        Assert.Equal(2, SystemFontDirectories.For(FontPlatform.MacOS, null, null, null).Count);
        Assert.Equal(2, SystemFontDirectories.For(FontPlatform.Unix, null, null, "").Count);
    }

    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 2)]
    public void ClassifiesThePlatform(bool isWindows, bool isMacOS, int expected)
    {
        Assert.Equal((FontPlatform)expected, SystemFontDirectories.Classify(isWindows, isMacOS));
    }

    [Fact]
    public void FindsTheWindowsFontsFolderUnderTheWindowsDirectoryWhenTheShellHasNone()
    {
        Assert.Equal(@"C:\Windows\Fonts", SystemFontDirectories.WindowsFontsFolder(@"C:\Windows\Fonts", @"D:\Other"));
        Assert.Equal(Path.Combine(@"D:\Windows", "Fonts"), SystemFontDirectories.WindowsFontsFolder("", @"D:\Windows"));
        Assert.Null(SystemFontDirectories.WindowsFontsFolder("", null));
    }

    [Fact]
    public void FindsFontFilesInSubfoldersAndSkipsEverythingElse()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        string top = folder.Write("b.TTF", [1]);
        string nested = folder.Write(Path.Combine("sub", "deeper", "a.otc"), [1]);
        folder.Write("notes.txt", [1]);
        folder.Write("c.woff2", [1]);
        string collection = folder.Write("a.ttc", [1]);
        string otf = folder.Write(Path.Combine("sub", "x.otf"), [1]);

        IReadOnlyList<string> files = FontFileEnumerator.Enumerate(
            [folder.Path, folder.Path, Path.Combine(folder.Path, "missing")]);

        Assert.Equal(new[] { collection, top, otf, nested }, files);
    }

    [Fact]
    public void RecognisesFontFileExtensions()
    {
        Assert.True(FontFileEnumerator.IsFontFile("font.TTC"));
        Assert.True(FontFileEnumerator.IsFontFile("font.otf"));
        Assert.False(FontFileEnumerator.IsFontFile("font.pfb"));
        Assert.False(FontFileEnumerator.IsFontFile("ttf"));
    }

    [Fact]
    public void SkipsFilesThatAreNotFontsItReads()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        byte[] georgian = TestFonts.Bytes(TestFonts.GeorgianFile);
        folder.Write("broken.ttf", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        folder.Write("truncated.ttf", georgian.Take(3000).ToArray());
        folder.Write("tiny.ttf", [0, 1]);
        folder.Write(Path.Combine("more", "georgian.ttf"), georgian);
        folder.Write("web.otf", new FontBytes().Tag("wOF2").Zeros(40).ToArray());

        SystemFontIndex index = new SystemFontIndex([folder.Path]);

        Assert.Equal(new[] { "NotoSansGeorgian-Regular" }, index.Faces.Select(face => face.Names.PostScriptName));
    }

    [Theory]
    [InlineData("LastResort")]
    [InlineData("Last Resort")]
    [InlineData("lastresort")]
    public void NeverFallsBackToALastResortFont(string family)
    {
        // Sorted by family, the last-resort font would come first; it claims the character but only draws a box.
        using TemporaryFolder folder = new TemporaryFolder();
        folder.Write("last.ttf", SyntheticFont.Named(family).Build());
        folder.Write("real.ttf", SyntheticFont.Named("Real").Build());

        FontFaceInfo? face = new SystemFontIndex([folder.Path]).FindCovering('A', new FaceStyle(400, 5, FontSlant.Upright));

        Assert.Equal("Real", face?.Names.PreferredFamily);
    }

    [Fact]
    public void ALastResortFontAloneIsNoFallback()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        folder.Write("last.ttf", SyntheticFont.Named("LastResort").Build());

        Assert.Null(new SystemFontIndex([folder.Path]).FindCovering('A', new FaceStyle(400, 5, FontSlant.Upright)));
    }

    [Fact]
    public void AFamilyThatMerelyContainsTheNameIsNoLastResort()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        folder.Write("last.ttf", SyntheticFont.Named("LastResort Sans").Build());

        Assert.NotNull(new SystemFontIndex([folder.Path]).FindCovering('A', new FaceStyle(400, 5, FontSlant.Upright)));
    }

    [Fact]
    public void DescribesAFontWithoutNamesOrOs2()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        folder.Write("plain.ttf", SyntheticFont.Minimal().With("head", SyntheticTables.Head(macStyle: 3)).Build());

        FontFaceInfo face = new SystemFontIndex([folder.Path]).Faces.Single();

        Assert.Same(FontNames.None, face.Names);
        Assert.Equal(new FaceStyle(700, 5, FontSlant.Italic), face.Style);
        Assert.True(face.Covers('A'));
    }

    [Fact]
    public void RejectsAFileWithoutAHeadTable()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        string path = folder.Write("headless.ttf", SyntheticFont.Minimal().Without("head").Build());

        Assert.Throws<FontFormatException>(() => FontFileScanner.Scan(path));
    }

    [Fact]
    public void ReadsCoverageFromTheCharacterMapAlone()
    {
        CharacterMap map = FontFileScanner.ReadCharacterMap(TestFonts.PathOf(TestFonts.CollectionFile), 1);

        Assert.Equal(34, map.GetGlyph('A'));
        Assert.Equal(0, map.GetGlyph('\uE000'));
    }

    [Theory]
    [InlineData("maxp")]
    [InlineData("cmap")]
    public void RejectsReadingCoverageWithoutTheTablesItNeeds(string tag)
    {
        using TemporaryFolder folder = new TemporaryFolder();
        byte[] font = SyntheticFont.Minimal().Build();

        // Rename the table in the directory, so the file still loads as far as the scanner goes.
        int count = BigEndian.UInt16(font, 4);
        int record = Enumerable.Range(0, count).Select(index => 12 + (16 * index))
            .Single(position => BigEndian.UInt32(font, position) == TableTag.FromString(tag));
        BigEndian.WriteUInt32(font, record, TableTag.FromString("zzzz"));
        string path = folder.Write("font.ttf", font);

        Assert.Throws<FontFormatException>(() => FontFileScanner.ReadCharacterMap(path, 0));
    }

    [Fact]
    public void CoversNothingOnceItsFileCannotBeRead()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        string path = folder.Write("font.ttf", TestFonts.Bytes(TestFonts.GeorgianFile));
        FontFaceInfo face = new SystemFontIndex([folder.Path]).Faces.Single();

        File.WriteAllBytes(path, [0, 1, 0, 0]);

        Assert.False(face.Covers('\u10D0'));
    }

    [Fact]
    public void CoversNothingOnceItsFileIsGone()
    {
        using TemporaryFolder folder = new TemporaryFolder();
        string path = folder.Write("font.ttf", TestFonts.Bytes(TestFonts.GeorgianFile));
        FontFaceInfo face = new SystemFontIndex([folder.Path]).Faces.Single();

        File.Delete(path);

        Assert.False(face.Covers('\u10D0'));
        Assert.Throws<FileNotFoundException>(() => face.Load());
    }

    [Fact]
    public void ReportsAFileThatShrinksWhileBeingRead()
    {
        // Claims twice the bytes it holds, as a file truncated between measuring and reading would.
        using ShrinkingStream stream = new ShrinkingStream(new byte[100]);

        Assert.Equal(40, FontFileScanner.ReadAt(stream, 10, 40).Length);
        Assert.Throws<FontFormatException>(() => FontFileScanner.ReadAt(stream, 90, 50));
        Assert.Throws<FontFormatException>(() => FontFileScanner.ReadAt(stream, 150, 60));
    }

    [Fact]
    public void SharesOneReadOfACollectionBetweenItsFaces()
    {
        FontFaceInfo[] faces = new SystemFontIndex([TestFonts.Directory]).Faces
            .Where(face => face.FilePath == TestFonts.PathOf(TestFonts.CollectionFile))
            .ToArray();

        OpenTypeFont regular = faces[0].Load();
        OpenTypeFont italic = faces[2].Load();

        Assert.True(regular.FileData.Span == italic.FileData.Span);
        Assert.Equal(2, italic.FaceIndex);
    }
}
