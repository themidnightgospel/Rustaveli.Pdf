using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Security;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Operations;

/// <summary>
/// Reading a file: its cross-reference sections through every update, its objects wherever they are kept, damage
/// repaired as viewers repair it, and its pages with what they inherit.
/// </summary>
public class PdfSourceTests
{
    private static readonly PdfName Marker = new PdfName("Marker");

    private static PdfSource Open(HandmadePdf pdf) => PdfSource.Open(pdf.ToArray());

    private static PdfValue Value(PdfSource source, int number) => (PdfValue)source.GetObject(number);

    /// <summary>A file the managed writer wrote, with a marked object among its pages.</summary>
    private static byte[] Written(PdfCrossReferenceFormat format, int pages = 2, PdfEncryption? encryption = null)
    {
        using MemoryStream output = new MemoryStream();
        using (PdfDocumentWriter writer = new PdfDocumentWriter(output, new PdfWriterOptions { CrossReferenceFormat = format, Encryption = encryption }))
        {
            for (int page = 1; page <= pages; page++)
            {
                PdfPage written = writer.BeginPage(100 * page, 200);
                written.Content.Rectangle(0, 0, page, page);
                written.Content.Fill();
                writer.EndPage(written);
            }

            writer.Catalog[Marker] = writer.File.Write(new PdfDictionary { [Marker] = PdfString.FromText("found") });
            writer.Finish();
        }

        return output.ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsWhatTheWriterWritesInEitherForm(bool streamed)
    {
        PdfSource source = PdfSource.Open(Written(streamed ? PdfCrossReferenceFormat.Stream : PdfCrossReferenceFormat.Table, pages: 3));

        Assert.False(source.WasRepaired);
        Assert.Equal(3, source.Pages.Count);
        Assert.Equal([100L, 200L, 300L], source.Pages.Select(page => source.Resolve(page.Dictionary[PdfNames.MediaBox]).AsArray()[2].AsInteger()));

        PdfDictionary marked = source.Resolve(source.Catalog[Marker]).AsDictionary();
        Assert.Equal("found", Encoding.ASCII.GetString(marked[Marker].AsString().Bytes.ToArray()));

        SourceStream content = source.Stream(source.Pages[1].Dictionary[PdfNames.Contents])!;
        Assert.Contains("0 0 2 2 re", Encoding.ASCII.GetString(source.Decode(content)), StringComparison.Ordinal);
    }

    private static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));

    /// <summary>
    /// <paramref name="text"/> encrypted as the standard handler encrypts a string of object <paramref name="number"/>,
    /// generation <paramref name="generation"/> (ISO 32000-1, algorithm 1): worked out here, apart from the reader.
    /// </summary>
    private static byte[] Encrypted(byte[] fileKey, int number, int generation, string text, bool aes)
    {
        byte[] salt = aes ? "sAlT"u8.ToArray() : [];
        byte[] input = [.. fileKey, (byte)number, (byte)(number >> 8), (byte)(number >> 16), (byte)generation, (byte)(generation >> 8), .. salt];
        byte[] key;

        using (MD5 md5 = MD5.Create())
            key = md5.ComputeHash(input).AsSpan(0, Math.Min(fileKey.Length + 5, 16)).ToArray();

        byte[] data = Encoding.ASCII.GetBytes(text);

        if (!aes)
            return Rc4.Transform(key, data);

        byte[] iv = new byte[16];
        return [.. iv, .. StandardSecurity.Aes(key, iv, data, true, CipherMode.CBC, PaddingMode.PKCS7)];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnObjectOfALaterGenerationIsDecryptedWithItsOwnKey(bool aes)
    {
        byte[] id = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        PdfDictionary written = PdfEncryption.Create(new Protection { OwnerPassword = "owner", Encryption = aes ? EncryptionLevel.AesWith128Bits : EncryptionLevel.Rc4With128Bits }, id).Dictionary;
        long version = written[new PdfName("V")].AsInteger();
        int revision = (int)written[new PdfName("R")].AsInteger();
        int permissions = (int)written[new PdfName("P")].AsInteger();
        byte[] owner = written[new PdfName("O")].AsString().Bytes.ToArray();
        byte[] user = written[new PdfName("U")].AsString().Bytes.ToArray();
        byte[] fileKey = StandardSecurity.FileKey(StandardSecurity.Pad(string.Empty), owner, permissions, id, revision, 16, encryptMetadata: true);
        string filters = aes ? "/CF<</StdCF<</CFM/AESV2/AuthEvent/DocOpen/Length 16>>>>/StmF/StdCF/StrF/StdCF" : string.Empty;

        HandmadePdf pdf = HandmadePdf.OnePage()
            .Object(4, $"<{Hex(Encrypted(fileKey, 4, 0, "generation zero", aes))}>")
            .Object(5, $"<</Filter/Standard/V {version}/R {revision}/Length 128{filters}/O<{Hex(owner)}>/U<{Hex(user)}>/P {permissions}>>")
            .Object(12, $"<{Hex(Encrypted(fileKey, 12, 1, "generation one", aes))}>", generation: 1);
        pdf.Section($"/Root 1 0 R/Encrypt 5 0 R/ID[<{Hex(id)}><{Hex(id)}>]");

        PdfSource source = Open(pdf);

        Assert.False(source.WasRepaired);
        Assert.Equal("generation zero", Encoding.ASCII.GetString(Value(source, 4).AsString().Bytes.ToArray()));
        Assert.Equal("generation one", Encoding.ASCII.GetString(Value(source, 12).AsString().Bytes.ToArray()));
    }

    [Fact]
    public void RefusesWhatIsNotAPdf()
    {
        UnreadableFileException exception = Assert.Throws<UnreadableFileException>(() => PdfSource.Open(Encoding.ASCII.GetBytes("GIF89a")));

        Assert.Contains("header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileWithNoCatalogAnywhereIsUnreadable() =>
        Assert.Throws<UnreadableFileException>(() => Open(new HandmadePdf().Object(1, "<</Type/Page>>").Raw("%%EOF")));

    [Fact]
    public void AnUpdateOverridesWhatCameBeforeAndFreesWhatItDeletes()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "(original)").Object(5, "(kept)").Object(6, "(deleted)");
        pdf.Section("/Root 1 0 R");
        pdf.Object(4, "(updated)");
        pdf.Raw("");
        string file = pdf.ToString();

        // The update lists object 4 anew and marks object 6 free.
        int table = file.Length;
        string update = file
            + "xref\n0 1\n0000000000 65535 f\r\n4 1\n" + file.LastIndexOf("4 0 obj", StringComparison.Ordinal).ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n\r\n"
            + "6 1\n0000000000 00001 f\r\n"
            + $"trailer\n<</Root 1 0 R/Size 7/Prev {file.IndexOf("xref", StringComparison.Ordinal)}>>\nstartxref\n{table}\n%%EOF\n";

        PdfSource source = PdfSource.Open(Encoding.Latin1.GetBytes(update));

        Assert.False(source.WasRepaired);
        Assert.Equal("updated", Encoding.ASCII.GetString(Value(source, 4).AsString().Bytes.ToArray()));
        Assert.Equal("kept", Encoding.ASCII.GetString(Value(source, 5).AsString().Bytes.ToArray()));
        Assert.Equal(PdfValueKind.Null, Value(source, 6).Kind);
        Assert.Equal([1, 2, 3, 4, 5], source.ObjectNumbers);
    }

    [Fact]
    public void AFirstSubsectionNumberedFromOneIsReadFromZero()
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        pdf.Section("/Root 1 0 R", startAtOne: true);

        PdfSource source = Open(pdf);

        Assert.False(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Fact]
    public void WrongOffsetsAreRepairedByScanning()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "(value)");
        pdf.Section("/Root 1 0 R", misplace: number => 9);

        PdfSource source = Open(pdf);

        Assert.Equal("value", Encoding.ASCII.GetString(Value(source, 4).AsString().Bytes.ToArray()));
        Assert.True(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Fact]
    public void AnObjectFoundElsewhereThanListedTriggersARepairOnce()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "(value)");
        string file = pdf.ToString();
        pdf.Section("/Root 1 0 R", misplace: number => number == 4 ? file.IndexOf("3 0 obj", StringComparison.Ordinal) : file.IndexOf($"{number} 0 obj", StringComparison.Ordinal));

        PdfSource source = Open(pdf);

        Assert.False(source.WasRepaired);
        Assert.Equal("value", Encoding.ASCII.GetString(Value(source, 4).AsString().Bytes.ToArray()));
        Assert.True(source.WasRepaired);
        Assert.Equal(PdfValueKind.Null, Value(source, 99).Kind);
    }

    [Fact]
    public void AFileWithNoCrossReferenceSectionIsReadByScanning()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "(old)").Object(4, "(newer)").Raw("trailer\n<</Root 1 0 R>>\n%%EOF\n");

        PdfSource source = Open(pdf);

        Assert.True(source.WasRepaired);
        Assert.Equal("newer", Encoding.ASCII.GetString(Value(source, 4).AsString().Bytes.ToArray()));
    }

    [Fact]
    public void WithoutATrailerTheCatalogIsFoundByItsType()
    {
        PdfSource source = Open(HandmadePdf.OnePage().Raw("%%EOF\n"));

        Assert.True(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Fact]
    public void ScanningSkipsWhatOnlyLooksLikeAnObject()
    {
        HandmadePdf pdf = HandmadePdf.OnePage()
            .Raw("% objects and objections: 7 0 objection\n")
            .Object(4, "(real)")
            .Raw("trailer<</Root 1 0 R>>trailer<<damaged\n");

        PdfSource source = Open(pdf);

        Assert.DoesNotContain(7, source.ObjectNumbers);
        Assert.Contains(4, source.ObjectNumbers);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("2")]
    [InlineData("-3")]
    [InlineData("5 0 R")]
    public void AStreamWhoseLengthIsWrongEndsAtEndstream(string length)
    {
        HandmadePdf pdf = HandmadePdf.OnePage("/Contents 4 0 R").Stream(4, "<<>>", "0 0 m 10 10 l S", length).Object(5, "7");
        pdf.Section("/Root 1 0 R");

        PdfSource source = Open(pdf);
        SourceStream content = (SourceStream)source.GetObject(4);

        Assert.Equal("0 0 m 10 10 l S", Encoding.ASCII.GetString(content.Data));
    }

    [Fact]
    public void AStreamKeepsItsLengthWhenRight()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Stream(4, "<<>>", "endstream inside\r\n", "18");
        pdf.Section("/Root 1 0 R");

        Assert.Equal("endstream inside\r\n", Encoding.ASCII.GetString(((SourceStream)Open(pdf).GetObject(4)).Data));
    }

    [Fact]
    public void AStreamWithNoEndIsUnreadable()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "<</Length 999>>\nstream\nforever");
        pdf.Section("/Root 1 0 R");

        Assert.Throws<UnreadableFileException>(() => Open(pdf).GetObject(4));
    }

    [Fact]
    public void ALengthReferringToItsOwnStreamIsNotFollowedForever()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Stream(4, "<<>>", "data", "4 0 R");
        pdf.Section("/Root 1 0 R");

        Assert.Equal("data", Encoding.ASCII.GetString(((SourceStream)Open(pdf).GetObject(4)).Data));
    }

    [Fact]
    public void PagesInheritResourcesBoxesAndRotationFromTheTree()
    {
        HandmadePdf pdf = new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 3/Resources<</Font<<>>>>/MediaBox[0 0 500 500]/Rotate 90>>")
            .Object(3, "<</Type/Page/Parent 2 0 R/Rotate 0>>")
            .Object(4, "<</Type/Pages/Parent 2 0 R/Kids[5 0 R 6 0 R]/Count 2/CropBox[1 1 2 2]>>")
            .Object(5, "<</Type/Page/Parent 4 0 R/MediaBox[0 0 10 10]>>")
            .Object(6, "<</Type/Page/Parent 4 0 R>>");
        pdf.Section("/Root 1 0 R");

        IReadOnlyList<SourcePage> pages = Open(pdf).Pages;

        Assert.Equal([3, 5, 6], pages.Select(page => page.ObjectNumber));
        Assert.Equal(0L, pages[0].Dictionary[new PdfName("Rotate")].AsInteger());
        Assert.Equal(90L, pages[1].Dictionary[new PdfName("Rotate")].AsInteger());
        Assert.Equal(10L, pages[1].Dictionary[PdfNames.MediaBox].AsArray()[2].AsInteger());
        Assert.Equal(500L, pages[2].Dictionary[PdfNames.MediaBox].AsArray()[2].AsInteger());
        Assert.True(pages[2].Dictionary.ContainsKey(new PdfName("CropBox")));
        Assert.False(pages[0].Dictionary.ContainsKey(new PdfName("CropBox")));
        Assert.All(pages, page => Assert.True(page.Dictionary.ContainsKey(PdfNames.Resources)));
    }

    [Fact]
    public void APageWithNoMediaBoxAnywhereIsLetter()
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        pdf.Section("/Root 1 0 R");

        PdfArray box = Open(pdf).Pages[0].Dictionary[PdfNames.MediaBox].AsArray();

        Assert.Equal([0L, 0L, 612L, 792L], box.Select(value => value.AsInteger()));
    }

    [Fact]
    public void APageTreeThatLoopsIsVisitedOnce()
    {
        HandmadePdf pdf = new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R 2 0 R 3 0 R 9 0 R 7]/Count 1>>")
            .Object(3, "<</Type/Page/Parent 2 0 R>>");
        pdf.Section("/Root 1 0 R");

        Assert.Single(Open(pdf).Pages);
    }

    [Fact]
    public void ACatalogWithoutAPageTreeIsUnreadable()
    {
        HandmadePdf pdf = new HandmadePdf().Object(1, "<</Type/Catalog>>");
        pdf.Section("/Root 1 0 R");

        Assert.Throws<UnreadableFileException>(() => Open(pdf).Pages);
    }

    [Fact]
    public void ReferencesAreFollowedAndStreamsGiveTheirDictionaries()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "5 0 R").Object(5, "6 0 R").Stream(6, "<</Kind/Data>>", "x").Object(7, "8 0 R").Object(8, "7 0 R");
        pdf.Section("/Root 1 0 R");
        PdfSource source = Open(pdf);

        Assert.Equal("Data", source.Resolve(new PdfReference(4)).AsDictionary()[new PdfName("Kind")].AsName().Value);
        Assert.Equal(PdfValueKind.Null, source.Resolve(new PdfReference(7)).Kind);
        Assert.Equal(12L, source.Resolve(12).AsInteger());
        Assert.NotNull(source.Stream(new PdfReference(6)));
        Assert.Null(source.Stream(new PdfReference(5)));
        Assert.Null(source.Stream(6));
    }

    [Theory]
    [InlineData(@"/W\[[^\]]*\]", "/X[1 2 2]")]
    [InlineData(@"/W\[[^\]]*\]", "/W 1")]
    [InlineData(@"/W\[[^\]]*\]", "/W[1 2]")]
    [InlineData(@"/W\[[^\]]*\]", "/W[1/A 2]")]
    [InlineData(@"/W\[[^\]]*\]", "/W[1 9 2]")]
    [InlineData(@"/W\[[^\]]*\]", "/W[0 0 0]")]
    [InlineData(@"/Size \d+", "/Sizx 1")]
    [InlineData(@"/Size \d+", "/Size/A")]
    public void ACrossReferenceStreamThatDoesNotSayHowToReadItIsRebuilt(string pattern, string replacement)
    {
        string text = Encoding.Latin1.GetString(Written(PdfCrossReferenceFormat.Stream));
        System.Text.RegularExpressions.Match found = System.Text.RegularExpressions.Regex.Match(text, pattern);

        // Replaced in place, the same length, so that the file is otherwise sound and only the section fails.
        Assert.True(found.Success && replacement.Length <= found.Length);
        PdfSource source = PdfSource.Open(Encoding.Latin1.GetBytes(text.Remove(found.Index, found.Length).Insert(found.Index, replacement.PadRight(found.Length))));

        Assert.True(source.WasRepaired);
        Assert.Equal(2, source.Pages.Count);
    }

    [Fact]
    public void ObjectStreamsAreFoundWhenTheSectionIsRebuilt()
    {
        byte[] file = Written(PdfCrossReferenceFormat.Stream);
        string text = Encoding.Latin1.GetString(file);

        // Losing the cross-reference stream leaves the objects kept in object streams to be found through them.
        byte[] damaged = Encoding.Latin1.GetBytes(text.Substring(0, text.LastIndexOf("startxref", StringComparison.Ordinal)).Replace("/Type/XRef", "/Type/Gone"));

        PdfSource source = PdfSource.Open(damaged);

        Assert.True(source.WasRepaired);
        Assert.Equal(2, source.Pages.Count);
        Assert.Equal("found", Encoding.ASCII.GetString(source.Resolve(source.Catalog[Marker]).AsDictionary()[Marker].AsString().Bytes.ToArray()));
    }

    [Theory]
    [InlineData(EncryptionLevel.Rc4With128Bits)]
    [InlineData(EncryptionLevel.AesWith128Bits)]
    [InlineData(EncryptionLevel.AesWith256Bits)]
    public void AnEncryptedFileWhoseSectionIsLostIsRebuiltAndDecrypted(EncryptionLevel level)
    {
        byte[] file = Written(PdfCrossReferenceFormat.Stream, encryption: PdfEncryption.Create(new Protection { UserPassword = "user", Encryption = level }));
        string text = Encoding.Latin1.GetString(file);
        int keyword = text.LastIndexOf("startxref", StringComparison.Ordinal);

        // Pointed at the header, the section is not found, and the file is read by scanning.
        PdfSource source = PdfSource.Open(Encoding.Latin1.GetBytes(text.Substring(0, keyword) + "startxref\n1\n%%EOF\n"), "user");

        Assert.True(source.WasRepaired);
        Assert.NotNull(source.Encryption);
        Assert.True(source.Trailer.ContainsKey(PdfNames.ID));
        Assert.Equal(2, source.Pages.Count);
        Assert.Equal("found", Encoding.ASCII.GetString(source.Resolve(source.Catalog[Marker]).AsDictionary()[Marker].AsString().Bytes.ToArray()));
        Assert.Throws<IncorrectPasswordException>(() => PdfSource.Open(Encoding.Latin1.GetBytes(text.Substring(0, keyword) + "startxref\n1\n%%EOF\n")));
    }

    [Fact]
    public void WhenTwoObjectStreamsHoldAnObjectTheLaterOneIsKept()
    {
        HandmadePdf pdf = HandmadePdf.OnePage()
            .Stream(5, "<</Type/ObjStm/N 1/First 4>>", "4 0 (old)")
            .Stream(6, "<</Type/ObjStm/N 1/First 4>>", "4 0 (new)")
            .Stream(7, "<</Type/ObjStm/N 1/First 4>>", "8 0 (only)")
            .Raw("trailer\n<</Root 1 0 R>>\n%%EOF\n");

        PdfSource source = Open(pdf);

        Assert.True(source.WasRepaired);
        Assert.Equal("new", Encoding.ASCII.GetString(Value(source, 4).AsString().Bytes.ToArray()));
        Assert.Equal("only", Encoding.ASCII.GetString(Value(source, 8).AsString().Bytes.ToArray()));
    }

    [Fact]
    public void AHybridFileFindsInItsStreamWhatItsTableMarksFree()
    {
        byte[] streamed = Written(PdfCrossReferenceFormat.Stream);
        string text = Encoding.Latin1.GetString(streamed);
        int stream = int.Parse(text.Substring(text.LastIndexOf("startxref", StringComparison.Ordinal) + 10).Split('\n')[0], System.Globalization.CultureInfo.InvariantCulture);
        string root = System.Text.RegularExpressions.Regex.Match(text.Substring(stream), @"/Root \d+ 0 R").Value;

        string hybrid = text.Substring(0, text.LastIndexOf("startxref", StringComparison.Ordinal))
            + $"xref\n0 1\n0000000000 65535 f\r\ntrailer\n<<{root}/Size 1/XRefStm {stream}>>\nstartxref\n{text.LastIndexOf("startxref", StringComparison.Ordinal)}\n%%EOF\n";

        PdfSource source = PdfSource.Open(Encoding.Latin1.GetBytes(hybrid));

        Assert.False(source.WasRepaired);
        Assert.Equal(2, source.Pages.Count);
    }
}
