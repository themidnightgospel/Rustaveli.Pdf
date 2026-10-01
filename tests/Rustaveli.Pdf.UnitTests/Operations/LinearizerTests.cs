using System.Text;
using Rustaveli.Pdf.Operations.Linearization;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Security;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>
/// Laying a file out for viewing as it downloads: what goes before the first page, with it, and after it.
/// </summary>
public class LinearizerTests
{
    private static readonly PdfName Outlines = new PdfName("Outlines");
    private static readonly PdfName First = new PdfName("First");
    private static readonly PdfName OpenAction = new PdfName("OpenAction");
    private static readonly PdfName ViewerPreferences = new PdfName("ViewerPreferences");
    private static readonly PdfName AcroForm = new PdfName("AcroForm");
    private static readonly PdfName DR = new PdfName("DR");
    private static readonly PdfName H = new PdfName("H");
    private static readonly PdfName E = new PdfName("E");
    private static readonly PdfName O = new PdfName("O");
    private static readonly PdfName N = new PdfName("N");
    private static readonly PdfName Title = new PdfName("Title");
    private static readonly PdfName Thumb = new PdfName("Thumb");

    private const string Outline = "<</Type/Outlines/First 9 0 R/Last 9 0 R/Count 1>>";

    private const string OutlineItem = "<</Title(One)/Parent 8 0 R/Dest[3 0 R/Fit]>>";

    /// <summary>A file of two pages sharing a font, each with content of its own.</summary>
    private static HandmadePdf TwoPages(string catalog = "", string firstPage = "") =>
        new HandmadePdf()
            .Object(1, $"<</Type/Catalog/Pages 2 0 R{catalog}>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>")
            .Object(3, $"<</Type/Page/Parent 2 0 R/Resources<</Font<</F1 5 0 R>>>>/Contents 6 0 R{firstPage}>>")
            .Object(4, "<</Type/Page/Parent 2 0 R/Resources<</Font<</F1 5 0 R>>>>/Contents 7 0 R>>")
            .Object(5, "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>")
            .Stream(6, "<<>>", "BT /F1 12 Tf (One) Tj ET")
            .Stream(7, "<<>>", "BT /F1 12 Tf (Two) Tj ET");

    private static byte[] Linearize(HandmadePdf file, string trailer = "/Root 1 0 R", PdfEncryption? encryption = null)
    {
        file.Section(trailer);

        using MemoryStream output = new MemoryStream();
        Linearizer.Write(file.ToArray(), encryption, output);
        return output.ToArray();
    }

    /// <summary>The dictionary of the object at <paramref name="offset"/>, or of the stream there.</summary>
    private static PdfDictionary DictionaryAt(byte[] file, long offset)
    {
        PdfParser parser = new PdfParser(file, (int)offset);

        Assert.True(parser.TryReadInteger(out _) && parser.TryReadInteger(out _) && parser.TryReadKeyword("obj"u8));
        return parser.ReadValue().AsDictionary();
    }

    /// <summary>The linearization dictionary: the first object, after the two lines of the header.</summary>
    private static PdfDictionary Parameters(byte[] file) =>
        DictionaryAt(file, Array.IndexOf(file, (byte)'\n', Array.IndexOf(file, (byte)'\n') + 1) + 1);

    private static long HintStreamAt(byte[] file) => Parameters(file)[H].AsArray()[0].AsInteger();

    private static PdfDictionary HintStream(byte[] file) => DictionaryAt(file, HintStreamAt(file));

    private static long FirstPageEnd(byte[] file) => Parameters(file)[E].AsInteger();

    private static string Text(byte[] file) => Encoding.Latin1.GetString(file);

    /// <summary>How many times object <paramref name="number"/> is written in the laid-out file.</summary>
    private static int Copies(byte[] file, int number) =>
        Text(file).Split([$"\n{number} 0 obj\n"], StringSplitOptions.None).Length - 1;

    /// <summary>Where object <paramref name="number"/> of the laid-out file begins.</summary>
    private static long OffsetOf(byte[] file, int number)
    {
        int found = Text(file).IndexOf($"\n{number} 0 obj\n", StringComparison.Ordinal);

        Assert.True(found >= 0, $"Object {number} is not in the file.");
        return found + 1;
    }

    private static int Number(PdfValue reference) => reference.AsReference().ObjectNumber;

    private static string TitleOf(PdfSource source, PdfValue item) =>
        Encoding.ASCII.GetString(source.Resolve(item).AsDictionary()[Title].AsString().Bytes.ToArray());

    [Fact]
    public void TheOutlineOfAFileThatOpensShowingItIsLaidOutWithTheFirstPage()
    {
        byte[] file = Linearize(TwoPages("/PageMode/UseOutlines/Outlines 8 0 R").Object(8, Outline).Object(9, OutlineItem));
        PdfSource source = PdfSource.Open(file);
        PdfValue outline = source.Catalog[Outlines];
        PdfValue item = source.Resolve(outline).AsDictionary()[First];

        Assert.True(HintStream(file).ContainsKey(O));
        Assert.InRange(OffsetOf(file, Number(outline)), HintStreamAt(file), FirstPageEnd(file));
        Assert.InRange(OffsetOf(file, Number(item)), HintStreamAt(file), FirstPageEnd(file));
        Assert.Equal("One", TitleOf(source, item));
    }

    [Theory]
    [InlineData("/Outlines 8 0 R")]
    [InlineData("/PageMode/UseNone/Outlines 8 0 R")]
    [InlineData("/PageMode(UseOutlines)/Outlines 8 0 R")]
    public void TheOutlineOfAFileThatOpensWithoutShowingItIsLaidOutAfterThePages(string catalog)
    {
        byte[] file = Linearize(TwoPages(catalog).Object(8, Outline).Object(9, OutlineItem));
        PdfSource source = PdfSource.Open(file);

        Assert.False(HintStream(file).ContainsKey(O));
        Assert.True(OffsetOf(file, Number(source.Catalog[Outlines])) > FirstPageEnd(file));
    }

    [Fact]
    public void AFileThatOpensShowingAnOutlineItDoesNotHaveIsLaidOutWithoutOne()
    {
        byte[] file = Linearize(TwoPages("/PageMode/UseOutlines"));

        Assert.False(HintStream(file).ContainsKey(O));
        Assert.Equal(2L, Parameters(file)[N].AsInteger());
        Assert.Equal(2, PdfSource.Open(file).Pages.Count);
    }

    [Fact]
    public void WhatTheFileNeedsToOpenIsLaidOutBeforeTheFirstPageOnceAndWithoutWhatThePageHas()
    {
        // The threads repeat the viewer preferences; the form's resources name the first page's font.
        HandmadePdf handmade = TwoPages("/OpenAction 8 0 R/ViewerPreferences 9 0 R/Threads[9 0 R]/AcroForm<</DR<</Font<</F1 5 0 R>>>>>>")
            .Object(8, "<</S/Named/N/NextPage>>")
            .Object(9, "<</HideToolbar true>>");

        byte[] file = Linearize(handmade);
        PdfDictionary catalog = PdfSource.Open(file).Catalog;
        int action = Number(catalog[OpenAction]);
        int preferences = Number(catalog[ViewerPreferences]);
        int font = Number(catalog[AcroForm].AsDictionary()[DR].AsDictionary()[PdfNames.Font].AsDictionary()[new PdfName("F1")]);

        Assert.True(OffsetOf(file, action) < HintStreamAt(file));
        Assert.True(OffsetOf(file, preferences) < HintStreamAt(file));
        Assert.Equal(1, Copies(file, preferences));
        Assert.InRange(OffsetOf(file, font), HintStreamAt(file), FirstPageEnd(file));
    }

    [Theory]
    [InlineData("/Info 8 0 R", true)]
    [InlineData("/Info 20 0 R", false)]
    [InlineData("/Info<</Title(Linear)>>", false)]
    [InlineData("", false)]
    public void TheTrailerRefersToTheInformationOnlyWhenItIsAnObjectTheFileHolds(string information, bool kept)
    {
        // Information given in the trailer itself is not the indirect reference the trailer's /Info has to be.
        byte[] file = Linearize(TwoPages().Object(8, "<</Title(Linear)>>"), "/Root 1 0 R" + information);
        PdfSource source = PdfSource.Open(file);

        Assert.Equal(kept, source.Trailer.TryGetValue(PdfNames.Info, out PdfValue info));

        if (kept)
            Assert.Equal("Linear", TitleOf(source, info));
    }

    /// <summary>The two halves of the laid-out file's identifier.</summary>
    private static (byte[] Permanent, byte[] Changing) Identifier(byte[] file)
    {
        PdfArray id = PdfSource.Open(file).Trailer[PdfNames.ID].AsArray();

        Assert.Equal(2, id.Count);
        return (id[0].AsString().Bytes.ToArray(), id[1].AsString().Bytes.ToArray());
    }

    [Fact]
    public void AFileKeepsThePermanentHalfOfItsIdentifier()
    {
        byte[] file = Linearize(TwoPages(), "/Root 1 0 R/ID[<00112233445566778899AABBCCDDEEFF><FFEEDDCCBBAA99887766554433221100>]");
        (byte[] permanent, byte[] changing) = Identifier(file);

        Assert.Equal<byte>([0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF], permanent);
        Assert.Equal(permanent, changing);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/ID(not a pair)")]
    [InlineData("/ID[]")]
    public void AFileWithoutAnIdentifierIsGivenANewOne(string identifier)
    {
        (byte[] permanent, byte[] changing) = Identifier(Linearize(TwoPages(), "/Root 1 0 R" + identifier));

        Assert.Equal(16, permanent.Length);
        Assert.Equal(permanent, changing);
    }

    [Fact]
    public void WhatLaterPagesShareIsLaidOutAfterEachOfThemAndWhatIsTheirOwn()
    {
        // The second and third pages share a font the first does not use.
        HandmadePdf handmade = new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R 4 0 R 5 0 R]/Count 3>>")
            .Object(3, "<</Type/Page/Parent 2 0 R/Contents 6 0 R>>")
            .Object(4, "<</Type/Page/Parent 2 0 R/Resources<</Font<</F2 9 0 R>>>>/Contents 7 0 R>>")
            .Object(5, "<</Type/Page/Parent 2 0 R/Resources<</Font<</F2 9 0 R>>>>/Contents 8 0 R>>")
            .Stream(6, "<<>>", "(One) Tj")
            .Stream(7, "<<>>", "(Two) Tj")
            .Stream(8, "<<>>", "(Three) Tj")
            .Object(9, "<</Type/Font/Subtype/Type1/BaseFont/Courier>>");

        byte[] file = Linearize(handmade);
        PdfSource source = PdfSource.Open(file);
        int shared = Number(source.Pages[1].Dictionary[PdfNames.Resources].AsDictionary()[PdfNames.Font].AsDictionary()[new PdfName("F2")]);

        Assert.Equal(3L, Parameters(file)[N].AsInteger());
        Assert.Equal(shared, Number(source.Pages[2].Dictionary[PdfNames.Resources].AsDictionary()[PdfNames.Font].AsDictionary()[new PdfName("F2")]));

        foreach (SourcePage page in source.Pages.Skip(1))
        {
            Assert.True(OffsetOf(file, shared) > OffsetOf(file, page.ObjectNumber));
            Assert.True(OffsetOf(file, shared) > OffsetOf(file, Number(page.Dictionary[PdfNames.Contents])));
        }
    }

    [Fact]
    public void AProtectedFileIsEncryptedAsItIsLaidOutUnderTheIdentifierItsKeyWasMadeFor()
    {
        PdfEncryption encryption = PdfEncryption.Create(new Protection { UserPassword = "web" });
        byte[] file = Linearize(TwoPages(), "/Root 1 0 R/ID[<00112233445566778899AABBCCDDEEFF><00112233445566778899AABBCCDDEEFF>]", encryption);

        Assert.DoesNotContain("(One) Tj", Text(file), StringComparison.Ordinal);
        Assert.Throws<IncorrectPasswordException>(() => PdfSource.Open(file));

        PdfSource source = PdfSource.Open(file, "web");
        SourceStream content = source.Stream(source.Pages[0].Dictionary[PdfNames.Contents])!;

        Assert.Equal(encryption.DocumentId, source.Trailer[PdfNames.ID].AsArray()[0].AsString().Bytes.ToArray());
        Assert.Equal("BT /F1 12 Tf (One) Tj ET", Encoding.ASCII.GetString(source.Decode(content)));
    }

    [Fact]
    public void AReferenceToAnObjectTheFileDoesNotHoldIsWrittenAsNull()
    {
        byte[] file = Linearize(TwoPages(firstPage: "/Thumb 20 0 R"));
        PdfSource source = PdfSource.Open(file);

        Assert.Contains("/Thumb null", Text(file), StringComparison.Ordinal);
        Assert.False(source.Pages[0].Dictionary.ContainsKey(Thumb));
        Assert.Equal(PdfValueKind.Dictionary, source.Resolve(source.Pages[0].Dictionary[PdfNames.Contents]).Kind);
    }

    [Fact]
    public void CrossReferenceAndObjectStreamsAreLeftOutAndEveryOtherStreamKept()
    {
        // Laid out anew, a file has cross-reference tables of its own; a stream typed by anything but a name is no such stream.
        HandmadePdf handmade = TwoPages()
            .Stream(8, "<</Type/XRef/Size 1/W[1 1 1]>>", "xref")
            .Stream(9, "<</Type/ObjStm/N 0/First 0>>", "objstm")
            .Stream(10, "<</Type(XRef)>>", "kept");

        string file = Text(Linearize(handmade));

        Assert.DoesNotContain("/Type/XRef", file, StringComparison.Ordinal);
        Assert.DoesNotContain("/Type/ObjStm", file, StringComparison.Ordinal);
        Assert.Contains("stream\nkept\nendstream", file, StringComparison.Ordinal);
    }
}
