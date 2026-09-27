using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class FontContainerTests
{
    [Fact]
    public void LoadsEveryFaceOfACollection()
    {
        IReadOnlyList<OpenTypeFont> faces = OpenTypeFont.LoadAll(TestFonts.Bytes(TestFonts.CollectionFile));

        Assert.Equal(3, faces.Count);
        Assert.Equal(new[] { 0, 1, 2 }, faces.Select(face => face.FaceIndex));
        Assert.Equal(
            new[] { "SpecimenSans-Regular", "SpecimenSans-SemiBold", "SpecimenSans-Italic" },
            faces.Select(face => face.Names.PostScriptName));

        // The faces share one file rather than each copying it.
        Assert.True(faces[0].FileData.Span == faces[2].FileData.Span);
    }

    [Fact]
    public void LoadsOneFaceOfACollectionByIndex()
    {
        OpenTypeFont face = OpenTypeFont.Load(TestFonts.Bytes(TestFonts.CollectionFile), 1);

        Assert.Equal(1, face.FaceIndex);
        Assert.Equal(600, face.Style.Weight);
        Assert.Equal(101, face.GlyphCount);
    }

    [Fact]
    public void TellsACollectionFromTooLittleDataToSay()
    {
        Assert.True(FontContainer.IsCollection(TestFonts.Bytes(TestFonts.CollectionFile)));
        Assert.False(FontContainer.IsCollection(TestFonts.Bytes(TestFonts.GeorgianFile)));
        Assert.False(FontContainer.IsCollection([(byte)'t', (byte)'t']));
    }

    [Fact]
    public void WritesACollectionFaceOutAsAFontOfItsOwn()
    {
        OpenTypeFont face = TestFonts.SpecimenItalic;

        byte[] standalone = face.ToStandaloneFile();
        OpenTypeFont reloaded = OpenTypeFont.Load(standalone);

        Assert.False(FontContainer.IsCollection(standalone));
        Assert.Equal("SpecimenSans-Italic", reloaded.Names.PostScriptName);
        Assert.Equal(face.GlyphCount, reloaded.GlyphCount);
        Assert.Equal(
            face.Tables.Records.Select(record => record.Tag), reloaded.Tables.Records.Select(record => record.Tag));
        Assert.Equal(face.MeasureWidthInUnits("AVATAR"), reloaded.MeasureWidthInUnits("AVATAR"));
        Assert.Equal(0xB1B0AFBA, SfntWriter.Checksum(standalone));
    }

    [Fact]
    public void LeavesOutACollectionFacesSignature()
    {
        SyntheticFont signed = SyntheticFont.Minimal().With("DSIG", [0, 0, 0, 1, 0, 0, 0, 0]);
        OpenTypeFont face = OpenTypeFont.Load(SyntheticFont.Collection(signed, signed), 1);

        OpenTypeFont standalone = OpenTypeFont.Load(face.ToStandaloneFile());

        Assert.True(face.Tables.Contains(TableTag.FromString("DSIG")));
        Assert.False(standalone.Tables.Contains(TableTag.FromString("DSIG")));
    }

    [Fact]
    public void GivesASingleFontFileAsItIs()
    {
        byte[] file = TestFonts.Bytes(TestFonts.CffFile);

        Assert.Equal(file, OpenTypeFont.Load(file).ToStandaloneFile());
    }

    [Fact]
    public void CountsOneFaceInAPlainFontFile()
    {
        Assert.Equal(1, FontContainer.CountFaces(TestFonts.Bytes(TestFonts.RegularFile)));
        Assert.Equal(3, FontContainer.CountFaces(TestFonts.Bytes(TestFonts.CollectionFile)));
        Assert.Single(OpenTypeFont.LoadAll(TestFonts.Bytes(TestFonts.RegularFile)));
    }

    [Fact]
    public void LoadsFromAStreamAndAFile()
    {
        using FileStream stream = File.OpenRead(TestFonts.PathOf(TestFonts.GeorgianFile));

        Assert.Equal(225, OpenTypeFont.Load(stream).GlyphCount);
        Assert.Equal(225, OpenTypeFont.LoadFile(TestFonts.PathOf(TestFonts.GeorgianFile)).GlyphCount);
    }

    [Fact]
    public void LoadsFromAStreamThatCannotSeek()
    {
        using NonSeekableStream stream = new NonSeekableStream(TestFonts.Bytes(TestFonts.GeorgianFile));

        Assert.Equal(225, OpenTypeFont.Load(stream).GlyphCount);
    }

    [Fact]
    public void BuildsFacesFromSyntheticCollections()
    {
        SyntheticFont first = SyntheticFont.Minimal();
        SyntheticFont second = SyntheticFont.Minimal().With("head", SyntheticTables.Head(unitsPerEm: 2048));

        IReadOnlyList<OpenTypeFont> faces = OpenTypeFont.LoadAll(SyntheticFont.Collection(first, second));

        Assert.Equal(new[] { 1000, 2048 }, faces.Select(face => face.UnitsPerEm));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RejectsAFaceIndexOutsideTheCollection(int faceIndex)
    {
        byte[] data = TestFonts.Bytes(TestFonts.CollectionFile);

        Assert.Throws<ArgumentOutOfRangeException>(() => OpenTypeFont.Load(data, faceIndex));
    }

    [Fact]
    public void RejectsAFaceIndexOtherThanZeroForAPlainFont()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OpenTypeFont.Load(SyntheticFont.Minimal().Build(), 1));
    }

    [Theory]
    [InlineData("wOFF")]
    [InlineData("wOF2")]
    [InlineData("typ1")]
    public void RejectsFormatsItDoesNotRead(string tag)
    {
        byte[] data = new FontBytes().Tag(tag).Zeros(60).ToArray();

        FontFormatException exception = Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(data));
        Assert.Contains("not", exception.Message);
    }

    [Fact]
    public void RejectsAnEmptyCollection()
    {
        byte[] data = new FontBytes().Tag("ttcf").U16(1).U16(0).U32(0).ToArray();

        Assert.Throws<FontFormatException>(() => OpenTypeFont.LoadAll(data));
    }

    [Fact]
    public void RejectsACollectionCountNoFileCouldHold()
    {
        byte[] data = new FontBytes().Tag("ttcf").U16(1).U16(0).U32(0xFFFFFFFF).ToArray();

        Assert.Throws<FontFormatException>(() => OpenTypeFont.LoadAll(data));
    }

    [Fact]
    public void RejectsAFaceCountTheFileHasNoRoomFor()
    {
        byte[] data = new FontBytes().Tag("ttcf").U16(1).U16(0).U32(100_000_000).U32(16).ToArray();

        Assert.Throws<FontFormatException>(() => OpenTypeFont.LoadAll(data));
    }

    [Fact]
    public void RejectsAFaceOffsetBeyondAnyFile()
    {
        byte[] data = new FontBytes().Tag("ttcf").U16(1).U16(0).U32(1).U32(0x80000000).ToArray();

        Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(data));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0, 1, 0 })]
    [InlineData(new byte[] { (byte)'t', (byte)'e', (byte)'x', (byte)'t', 0, 0, 0, 0, 0, 0, 0, 0 })]
    public void RejectsDataThatIsNotAFont(byte[] data)
    {
        Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(data));
    }

    [Theory]
    [InlineData("head")]
    [InlineData("hhea")]
    [InlineData("maxp")]
    [InlineData("hmtx")]
    [InlineData("cmap")]
    public void RequiresTheTablesEveryFontNeeds(string tag)
    {
        byte[] data = SyntheticFont.Minimal().Without(tag).Build();

        FontFormatException exception = Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(data));
        Assert.Contains(tag, exception.Message);
    }

    [Fact]
    public void AcceptsTheAppleTrueTypeVersionTag()
    {
        SyntheticFont font = SyntheticFont.Minimal();
        font.Version = "true";

        Assert.Equal(OutlineFormat.TrueType, font.Load().Outlines);
    }

    [Fact]
    public void TellsOutlineFormatsApart()
    {
        SyntheticFont cff = SyntheticFont.Minimal().Without("glyf").Without("loca")
            .With("CFF ", SyntheticLayout.Cff("Test", 3));
        cff.Version = "OTTO";
        SyntheticFont cff2 = SyntheticFont.Minimal().Without("glyf").Without("loca").With("CFF2", [2, 0, 5, 0, 0]);
        SyntheticFont bitmap = SyntheticFont.Minimal().Without("glyf").Without("loca");

        Assert.Equal(OutlineFormat.Cff, cff.Load().Outlines);
        Assert.Equal(OutlineFormat.Cff2, cff2.Load().Outlines);
        Assert.Equal(OutlineFormat.None, bitmap.Load().Outlines);
        Assert.Null(cff2.Load().Glyphs);
        Assert.Null(cff2.Load().Cff);
        Assert.False(bitmap.Load().TryGetGlyphBounds(1, out _));
    }

    [Fact]
    public void KeepsTheFirstOfADuplicatedTable()
    {
        byte[] font = SyntheticFont.Minimal().Build();

        // Rename the 'loca' record, which the directory lists after 'head', so 'head' appears twice.
        int count = BigEndian.UInt16(font, 4);
        int loca = Enumerable.Range(0, count).Select(index => 12 + (16 * index))
            .Single(record => BigEndian.UInt32(font, record) == TableTag.Loca);
        BigEndian.WriteUInt32(font, loca, TableTag.Head);

        TableDirectory directory = TableDirectory.Read(font, 0, font.Length);

        Assert.Equal(count - 1, directory.Records.Count);
        Assert.True(directory.TryGet(TableTag.Head, out TableRecord head));
        Assert.Equal(54, head.Length);
        Assert.False(directory.Contains(TableTag.Loca));
    }

    [Fact]
    public void RejectsATableThatRunsPastTheEndOfTheFile()
    {
        byte[] font = SyntheticFont.Minimal().Build();
        BigEndian.WriteUInt32(font, 12 + 12, (uint)font.Length);

        Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(font));
    }

    [Fact]
    public void RejectsADirectoryLongerThanTheFile()
    {
        byte[] font = new FontBytes().U32(0x00010000).U16(4000).Zeros(6).ToArray();

        Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(font));
    }

    [Fact]
    public void ConvertsTagsBothWays()
    {
        Assert.Equal(TableTag.Os2, TableTag.FromString("OS/2"));
        Assert.Equal("CFF ", TableTag.ToString(TableTag.Cff));
        Assert.Throws<ArgumentException>(() => TableTag.FromString("toolong"));
    }
}
