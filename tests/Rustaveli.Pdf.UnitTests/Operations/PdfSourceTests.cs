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
    private static byte[] Encrypted(byte[] fileKey, int number, int generation, string text, bool aes) =>
        Encrypted(fileKey, number, generation, Encoding.Latin1.GetBytes(text), aes);

    private static byte[] Encrypted(byte[] fileKey, int number, int generation, byte[] data, bool aes)
    {
        byte[] salt = aes ? "sAlT"u8.ToArray() : [];
        byte[] input = [.. fileKey, (byte)number, (byte)(number >> 8), (byte)(number >> 16), (byte)generation, (byte)(generation >> 8), .. salt];
        byte[] key;

        using (MD5 md5 = MD5.Create())
            key = md5.ComputeHash(input).AsSpan(0, Math.Min(fileKey.Length + 5, 16)).ToArray();

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

    /// <summary>
    /// A file encrypted as Acrobat's "encrypt only file attachments" encrypts it: strings and streams left plain, the
    /// attached file (object 6) encrypted with AES under a filter opened by the password "att" only when an attachment is,
    /// and two streams that name that filter themselves, one plain beneath it (object 8) and one deflated (object 9).
    /// </summary>
    private static HandmadePdf AttachmentsOnly(
        string filters = "/CF<</StdCF<</CFM/AESV2/AuthEvent/EFOpen/Length 16>>>>/StmF/Identity/StrF/Identity/EFF/StdCF", string attachment = "attached")
    {
        byte[] id = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        PdfDictionary written = PdfEncryption.Create(new Protection { UserPassword = "att", OwnerPassword = "own", Encryption = EncryptionLevel.AesWith128Bits }, id).Dictionary;
        int permissions = (int)written[new PdfName("P")].AsInteger();
        byte[] owner = written[new PdfName("O")].AsString().Bytes.ToArray();
        byte[] user = written[new PdfName("U")].AsString().Bytes.ToArray();
        byte[] fileKey = StandardSecurity.FileKey(StandardSecurity.Pad("att"), owner, permissions, id, 4, 16, encryptMetadata: true);

        string Stream(int number, string text) => Encoding.Latin1.GetString(Encrypted(fileKey, number, 0, text, aes: true));

        byte[] deflated;
        using (MemoryStream compressed = new MemoryStream())
        {
            using (System.IO.Compression.DeflateStream deflater = new System.IO.Compression.DeflateStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                deflater.Write("deflated beneath"u8.ToArray(), 0, 16);

            deflated = compressed.ToArray();
        }

        HandmadePdf pdf = new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R/Names<</EmbeddedFiles<</Names[(a.txt) 5 0 R]>>>>>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>")
            .Object(3, "<</Type/Page/Parent 2 0 R/Contents 4 0 R>>")
            .Stream(4, "<<>>", "0 0 m 9 9 l S")
            .Object(5, "<</Type/Filespec/F(a.txt)/EF<</F 6 0 R>>>>")
            .Stream(6, "<</Type/EmbeddedFile>>", Stream(6, attachment))
            .Object(7, $"<</Filter/Standard/V 4/R 4/Length 128{filters}/O<{Hex(owner)}>/U<{Hex(user)}>/P {permissions}>>")
            .Stream(8, "<</DecodeParms<</Type/CryptFilterDecodeParms/Name/StdCF>>/Filter/Crypt>>", Stream(8, "own filter"))
            .Stream(9, "<</Filter[/Crypt/FlateDecode]/DecodeParms[<</Name/StdCF>> null]>>", Encoding.Latin1.GetString(Encrypted(fileKey, 9, 0, deflated, aes: true)))
            .Stream(10, "<</Filter/Crypt>>", "identity")
            .Stream(11, "<</DecodeParms<</Name/Unknown>>/Filter[/Crypt]>>", "unknown");
        pdf.Section($"/Root 1 0 R/Encrypt 7 0 R/ID[<{Hex(id)}><{Hex(id)}>]");
        return pdf;
    }

    [Fact]
    public void AFileWhoseAttachmentsAloneAreEncryptedOpensWithoutAPassword()
    {
        PdfSource source = Open(AttachmentsOnly());

        Assert.NotNull(source.Encryption);
        Assert.Equal("0 0 m 9 9 l S", Encoding.ASCII.GetString(source.Decode(source.Stream(source.Pages[0].Dictionary[PdfNames.Contents])!)));

        // Without the password the attachment stays as it is, and says so.
        SourceStream attached = (SourceStream)source.GetObject(6);
        Assert.Equal(32, attached.Data.Length);
        Assert.True(((SourceStream)source.GetObject(8)).Dictionary.ContainsKey(PdfNames.Filter));
    }

    /// <summary>The text of the one embedded file in <paramref name="source"/>, decoded.</summary>
    private static string Attachment(PdfSource source) =>
        Encoding.ASCII.GetString(source.Decode(source.ObjectNumbers.Select(source.GetObject).OfType<SourceStream>()
            .Single(stream => stream.Dictionary.TryGetValue(PdfNames.Type, out PdfValue type) && type.AsName().Value == "EmbeddedFile")));

    /// <summary>A file whose attachment alone is encrypted, with 256-bit AES, whose keys do not depend on object numbers.</summary>
    private static byte[] AttachmentsOnlyAes256()
    {
        byte[] id = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        PdfEncryption created = PdfEncryption.Create(new Protection { UserPassword = "att", OwnerPassword = "own", Encryption = EncryptionLevel.AesWith256Bits }, id);
        PdfDictionary written = created.Dictionary;
        string Entry(string key) => Hex(written[new PdfName(key)].AsString().Bytes.ToArray());

        HandmadePdf pdf = new HandmadePdf()
            .Object(1, "<</Type/Catalog/Pages 2 0 R/Names<</EmbeddedFiles<</Names[(a.txt) 5 0 R]>>>>>>")
            .Object(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>")
            .Object(3, "<</Type/Page/Parent 2 0 R/Contents 4 0 R>>")
            .Stream(4, "<<>>", "0 0 m 9 9 l S")
            .Object(5, "<</Type/Filespec/F(a.txt)/EF<</F 6 0 R>>>>")
            .Stream(6, "<</Type/EmbeddedFile>>", Encoding.Latin1.GetString(created.EncryptStream(6, new PdfDictionary(), "attached"u8)))
            .Object(7, "<</Filter/Standard/V 5/R 6/Length 256/CF<</StdCF<</CFM/AESV3/AuthEvent/EFOpen/Length 32>>>>/StmF/Identity/StrF/Identity/EFF/StdCF"
                + $"/O<{Entry("O")}>/U<{Entry("U")}>/OE<{Entry("OE")}>/UE<{Entry("UE")}>/Perms<{Entry("Perms")}>/P {written[new PdfName("P")].AsInteger()}>>");
        pdf.Section($"/Root 1 0 R/Encrypt 7 0 R/ID[<{Hex(id)}><{Hex(id)}>]");
        return pdf.ToArray();
    }

    [Fact]
    public void AttachmentsAloneEncryptedStayReadableWhenTheFileIsSavedProtectedAsItWas()
    {
        // Longer than an AES block, so that data left unencrypted cannot pass for encrypted data too short to decrypt.
        const string Text = "an attachment longer than a block of the cipher";
        byte[] saved = PdfFile.Open(AttachmentsOnly(attachment: Text).ToArray(), "att").ToArray();

        // Written encrypted, as the encryption dictionary the file keeps says it is, and read back as it was.
        Assert.DoesNotContain(Text, Encoding.Latin1.GetString(saved), StringComparison.Ordinal);
        Assert.Equal(Text, Attachment(PdfSource.Open(saved, "att")));
    }

    [Fact]
    public void AttachmentsWhoseKeyNamesTheirObjectCannotBeCarriedWithoutThePassword()
    {
        // Under 128-bit AES an object's key holds its number, which saving changes: without the password the
        // attachment can be neither decrypted nor encrypted again, so saving would write it unreadable.
        InvalidOperationException kept = Assert.Throws<InvalidOperationException>(() => PdfFile.Open(AttachmentsOnly().ToArray()).ToArray());
        Assert.Throws<InvalidOperationException>(() => PdfFile.Open(AttachmentsOnly().ToArray()).Unprotect().ToArray());

        Assert.Contains("password", kept.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Aes256AttachmentsAreCarriedAsTheyAreWithoutThePassword()
    {
        byte[] file = AttachmentsOnlyAes256();

        // Opened with the password, and without it: under 256-bit AES the attachment's key does not depend on where it
        // is written, so it is carried encrypted as it was when the file keeps its protection.
        Assert.Equal("attached", Attachment(PdfSource.Open(PdfFile.Open(file, "att").ToArray(), "att")));
        Assert.Equal("attached", Attachment(PdfSource.Open(PdfFile.Open(file).ToArray(), "att")));
        Assert.Throws<InvalidOperationException>(() => PdfFile.Open(file).Unprotect().ToArray());
    }

    [Fact]
    public void AttachmentsAndStreamsNamingTheirOwnCryptFilterAreDecryptedWithIt()
    {
        PdfSource source = PdfSource.Open(AttachmentsOnly().ToArray(), "att");

        SourceStream attached = (SourceStream)source.GetObject(6);
        SourceStream own = (SourceStream)source.GetObject(8);
        SourceStream deflated = (SourceStream)source.GetObject(9);

        Assert.Equal("attached", Encoding.ASCII.GetString(attached.Data));
        Assert.Equal("own filter", Encoding.ASCII.GetString(own.Data));
        Assert.False(own.Dictionary.ContainsKey(PdfNames.Filter));
        Assert.False(own.Dictionary.ContainsKey(PdfNames.DecodeParms));
        Assert.Equal("deflated beneath", Encoding.ASCII.GetString(source.Decode(deflated)));
        Assert.Equal(["FlateDecode"], source.Resolve(deflated.Dictionary[PdfNames.Filter]).AsArray().Cast<PdfValue>().Select(filter => filter.AsName().Value));

        // Saved without protection, the attachment is plain.
        PdfSource plain = PdfSource.Open(PdfFile.Open(AttachmentsOnly().ToArray(), "att").Unprotect().ToArray());

        Assert.Null(plain.Encryption);
        Assert.Equal("attached", Attachment(plain));

        // A crypt filter named by no name is the identity; one the file does not define is taken as its streams' filter.
        foreach ((int number, string text) in new[] { (10, "identity"), (11, "unknown") })
        {
            SourceStream stream = (SourceStream)source.GetObject(number);
            Assert.Equal(text, Encoding.ASCII.GetString(stream.Data));
            Assert.False(stream.Dictionary.ContainsKey(PdfNames.Filter));
            Assert.False(stream.Dictionary.ContainsKey(PdfNames.DecodeParms));
        }
    }

    [Theory]
    [InlineData("/CF<</StdCF<</CFM/AESV2/AuthEvent/DocOpen/Length 16>>>>/StmF/Identity/StrF/Identity/EFF/StdCF")]
    [InlineData("/CF<</StdCF<</CFM/AESV2/Length 16>>>>/StmF/Identity/StrF/Identity/EFF/StdCF")]
    [InlineData("/CF<</StdCF<</CFM/AESV2/AuthEvent/EFOpen/Length 16>>>>/StmF/Identity/StrF/Identity/EFF/Other")]
    [InlineData("/CF<</StdCF<</CFM/AESV2/AuthEvent/EFOpen/Length 16>>>>/StmF/Identity/StrF/Identity/EFF 7")]
    [InlineData("/CF<</StdCF<</CFM/AESV2/AuthEvent/EFOpen/Length 16>>>>/StmF/Identity/StrF/Identity")]
    [InlineData("/CF<</StdCF<</CFM/AESV2/AuthEvent/EFOpen/Length 16>>>>/StmF/Identity/StrF/StdCF/EFF/StdCF")]
    [InlineData("/CF<</StdCF<</CFM/AESV2/AuthEvent/EFOpen/Length 16>>>>/StmF/StdCF/StrF/Identity/EFF/StdCF")]
    public void AFileWhoseAttachmentsAloneAreEncryptedButNotAsAcrobatAsksStillNeedsItsPassword(string filters) =>
        Assert.Throws<IncorrectPasswordException>(() => Open(AttachmentsOnly(filters)));

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
    [InlineData("/Type/XRef", "/Tipe/XRef")]
    [InlineData("/Type/XRef", "/Type 1")]
    [InlineData("/Type/XRef", "/Type/XRaf")]
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

    [Theory]
    [InlineData("<</Type/ObjStm/N 1/First -20>>", "4 0 (lost)")]
    [InlineData("<</Type/ObjStm/N 1/First 4>>", "4 4294967290 (lost)")]
    [InlineData("<</Type/ObjStm/N 1/First 2147483647>>", "4 10 (lost)")]
    [InlineData("<</Type/ObjStm/N 1/First 4294967296>>", "4 0 (lost)")]
    public void AnObjectStreamPlacingAnObjectOutsideItsDataIsDamageNotACrash(string dictionary, string data)
    {
        // Found by fuzzing: a negative /First put an object before the start of the stream's data. Offsets and a /First
        // too large for 32 bits must not wrap round to the same place.
        HandmadePdf pdf = HandmadePdf.OnePage()
            .Stream(5, dictionary, data)
            .Raw("trailer\n<</Root 1 0 R>>\n%%EOF\n");

        PdfSource source = Open(pdf);

        Assert.True(source.WasRepaired);
        Assert.Equal(PdfValueKind.Null, Value(source, 4).Kind);
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

    private static string Text(PdfSource source, int number) => Encoding.ASCII.GetString(Value(source, number).AsString().Bytes.ToArray());

    /// <summary>A row of a cross-reference stream placing object <paramref name="number"/> where it was written.</summary>
    private static long[] InUse(HandmadePdf pdf, int number) => [1, pdf.OffsetOf(number), 0];

    /// <summary>
    /// The standard handler's dictionary for 128-bit RC4 under an empty user password and the identifier
    /// <paramref name="id"/>, and the file key it opens with.
    /// </summary>
    private static (string Dictionary, byte[] FileKey) Rc4Encryption(byte[] id)
    {
        PdfDictionary written = PdfEncryption.Create(new Protection { OwnerPassword = "owner", Encryption = EncryptionLevel.Rc4With128Bits }, id).Dictionary;
        long version = written[new PdfName("V")].AsInteger();
        int revision = (int)written[new PdfName("R")].AsInteger();
        int permissions = (int)written[new PdfName("P")].AsInteger();
        byte[] owner = written[new PdfName("O")].AsString().Bytes.ToArray();
        byte[] user = written[new PdfName("U")].AsString().Bytes.ToArray();
        byte[] fileKey = StandardSecurity.FileKey(StandardSecurity.Pad(string.Empty), owner, permissions, id, revision, 16, encryptMetadata: true);

        return ($"<</Filter/Standard/V {version}/R {revision}/Length 128/O<{Hex(owner)}>/U<{Hex(user)}>/P {permissions}>>", fileKey);
    }

    [Fact]
    public void ARootThatIsNotADictionaryIsUnreadable()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "(not a catalog)");
        pdf.Section("/Root 4 0 R");

        UnreadableFileException exception = Assert.Throws<UnreadableFileException>(() => Open(pdf));

        Assert.Contains("catalog", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEncryptionDictionaryWrittenInTheTrailerDecryptsEveryObject()
    {
        byte[] id = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        (string encrypt, byte[] fileKey) = Rc4Encryption(id);
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, $"<{Hex(Encrypted(fileKey, 4, 0, "secret", aes: false))}>");
        pdf.Section($"/Root 1 0 R/Encrypt{encrypt}/ID[<{Hex(id)}><{Hex(id)}>]");

        PdfSource source = Open(pdf);

        Assert.NotNull(source.Encryption);
        Assert.Equal("secret", Text(source, 4));
    }

    [Fact]
    public void AnEncryptionDictionaryThatIsMissingIsUnreadable()
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        pdf.Section("/Root 1 0 R/Encrypt 9 0 R");

        UnreadableFileException exception = Assert.Throws<UnreadableFileException>(() => Open(pdf));

        Assert.Contains("encryption dictionary", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/ID 7")]
    [InlineData("/ID[]")]
    [InlineData("/ID[7 8]")]
    public void AFileWithoutAnIdentifierStringIsDecryptedWithAnEmptyOne(string identifier)
    {
        (string encrypt, byte[] fileKey) = Rc4Encryption([]);
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, $"<{Hex(Encrypted(fileKey, 4, 0, "secret", aes: false))}>").Object(5, encrypt);
        pdf.Section($"/Root 1 0 R/Encrypt 5 0 R{identifier}");

        PdfSource source = Open(pdf);

        Assert.NotNull(source.Encryption);
        Assert.Equal("secret", Text(source, 4));
    }

    [Fact]
    public void ObjectStreamsOfARebuiltFileAreFoundAgainOnceItIsDecrypted()
    {
        // Its streams left plain, the object stream is read before the encryption is known, and read again after.
        HandmadePdf pdf = AttachmentsOnly()
            .Stream(12, "<</Type/ObjStm/N 1/First 5>>", "13 0 (packed)")
            .Raw("startxref\n1\n%%EOF\n");

        PdfSource source = Open(pdf);

        Assert.True(source.WasRepaired);
        Assert.NotNull(source.Encryption);
        Assert.Equal("packed", Text(source, 13));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(1_000_000L)]
    public void AnOffsetOutsideTheFileIsRepairedByScanning(long offset)
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "(value)");
        pdf.StreamSection(5, "/Size 5/Root 1 0 R", [1, 8, 2], [0, 0, 0], InUse(pdf, 1), InUse(pdf, 2), InUse(pdf, 3), [1, offset, 0]);

        PdfSource source = Open(pdf);

        Assert.False(source.WasRepaired);
        Assert.Equal("value", Text(source, 4));
        Assert.True(source.WasRepaired);
    }

    [Theory]
    [InlineData("%x\n")]
    [InlineData("%4 x\n")]
    [InlineData("%4 0 R\n")]
    public void AnOffsetWhereNoObjectHeaderBeginsIsRepairedByScanning(string decoy)
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Raw(decoy).Object(4, "(value)");
        int at = pdf.ToString().IndexOf(decoy, StringComparison.Ordinal) + 1;
        pdf.Section("/Root 1 0 R", misplace: number => number == 4 ? at : pdf.OffsetOf(number));

        PdfSource source = Open(pdf);

        Assert.Equal("value", Text(source, 4));
        Assert.True(source.WasRepaired);
    }

    [Theory]
    [InlineData("<</Length 4>>\nstream\r\ndata\nendstream", "data")]
    [InlineData("<</Length 4>>\nstream\rdata\nendstream", "data")]
    [InlineData("<</Length 99>>\nstream\ndata\r\nendstream", "data")]
    [InlineData("<</Length 99>>\nstream\ndata\rendstream", "data")]
    [InlineData("<</Length 99>>\nstream\ndata endstream", "data ")]
    [InlineData("<</Length 99>>\nstream\nendstream", "")]
    public void AStreamsDataLeavesOutTheLineEndsAroundIt(string body, string data)
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, body);
        pdf.Section("/Root 1 0 R");

        Assert.Equal(data, Encoding.ASCII.GetString(((SourceStream)Open(pdf).GetObject(4)).Data));
    }

    /// <summary>
    /// One page, then a cross-reference table listing it and an object 4 written after the table, then
    /// <paramref name="last"/>, with which the file ends.
    /// </summary>
    private static byte[] EndingWithObjectFour(string last)
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        string file = pdf.ToString();

        string Section(int four) =>
            "xref\n0 5\n0000000000 65535 f\r\n"
            + string.Concat(new[] { pdf.OffsetOf(1), pdf.OffsetOf(2), pdf.OffsetOf(3), four }.Select(offset => offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n"))
            + $"trailer\n<</Root 1 0 R/Size 5>>\nstartxref\n{file.Length}\n%%EOF\n";

        return Encoding.Latin1.GetBytes(file + Section(file.Length + Section(0).Length) + last);
    }

    [Theory]
    [InlineData("stream")]
    [InlineData("stream\r")]
    public void AStreamCutOffAfterItsKeywordIsUnreadable(string keyword)
    {
        PdfSource source = PdfSource.Open(EndingWithObjectFour("4 0 obj\n<</Length 0>>\n" + keyword));

        Assert.Single(source.Pages);
        Assert.Throws<UnreadableFileException>(() => source.GetObject(4));
    }

    [Fact]
    public void AStartxrefWithoutAnOffsetIsRebuilt()
    {
        PdfSource source = Open(HandmadePdf.OnePage().Raw("trailer\n<</Root 1 0 R>>\nstartxref\nnowhere\n%%EOF\n"));

        Assert.True(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Theory]
    [InlineData("xref\n0 2\n0000000000 65535 f\r\nabcdefghij 00000 n\r\ntrailer\n<</Root 1 0 R>>\n")]
    [InlineData("xref\n0 2\n0000000000 65535 f\r\n-000000009 00000 n\r\ntrailer\n<</Root 1 0 R>>\n")]
    [InlineData("xref\n0 2\n0000000000 65535 f\r\n0000000009 abcde n\r\ntrailer\n<</Root 1 0 R>>\n")]
    [InlineData("xref\n0 2\n0000000000 65535 f\r\n0000000009 00000 x\r\ntrailer\n<</Root 1 0 R>>\n")]
    [InlineData("xref\n0 1\n0000000000 65535 f\r\n")]
    public void ACrossReferenceTableThatCannotBeReadIsRebuilt(string table)
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        int offset = pdf.ToString().Length;

        PdfSource source = Open(pdf.Raw($"{table}startxref\n{offset}\n%%EOF\n"));

        Assert.True(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASubsectionThatTrulyBeginsAtObjectOneIsNotRenumbered(bool catalogFirst)
    {
        // Written before the header, the catalog is at offset 0, as the free head of a misnumbered subsection would be.
        const string Catalog = "1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n";
        HandmadePdf pdf = new HandmadePdf().Raw(catalogFirst ? string.Empty : Catalog)
            .Object(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>")
            .Object(3, "<</Type/Page/Parent 2 0 R>>");
        string prefix = catalogFirst ? Catalog : string.Empty;
        string file = prefix + pdf.ToString();
        long[] offsets = [catalogFirst ? 0 : "%PDF-1.7\n".Length, prefix.Length + pdf.OffsetOf(2), prefix.Length + pdf.OffsetOf(3)];

        string section = "xref\n0 1\n0000000000 65535 f\r\n1 3\n"
            + string.Concat(offsets.Select(offset => offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n"))
            + $"trailer\n<</Root 1 0 R/Size 4>>\nstartxref\n{file.Length}\n%%EOF\n";

        PdfSource source = PdfSource.Open(Encoding.Latin1.GetBytes(file + section));

        Assert.False(source.WasRepaired);
        Assert.Equal([1, 2, 3], source.ObjectNumbers);
        Assert.Single(source.Pages);
    }

    [Fact]
    public void AnObjectAtTheVeryStartOfTheFileIsFoundByScanning()
    {
        HandmadePdf rest = new HandmadePdf()
            .Object(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>")
            .Object(3, "<</Type/Page/Parent 2 0 R>>")
            .Raw("%%EOF\n");

        PdfSource source = PdfSource.Open(Encoding.Latin1.GetBytes("1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n" + rest));

        Assert.True(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Theory]
    [InlineData("%50 obj\n")]
    [InlineData("/A 0 obj\n")]
    [InlineData("x5 0 obj\n")]
    [InlineData("0 0 obj\n(zero)\nendobj\n")]
    [InlineData("99999999999 0 obj\n(too large)\nendobj\n")]
    [InlineData("%%EOF obj")]
    public void ScanningTakesOnlyObjectsNumberedWithinRange(string text)
    {
        PdfSource source = Open(HandmadePdf.OnePage().Raw("trailer\n<</Root 1 0 R>>\n").Raw(text));

        Assert.True(source.WasRepaired);
        Assert.Equal([1, 2, 3], source.ObjectNumbers);
    }

    [Theory]
    [InlineData("/XRefStm/None")]
    [InlineData("/XRefStm 3 0 R")]
    public void AHybridOffsetThatIsNotANumberIsIgnored(string hybrid)
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        pdf.Section("/Root 1 0 R" + hybrid);

        PdfSource source = Open(pdf);

        Assert.False(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Theory]
    [InlineData("5 x\n")]
    [InlineData("5 0 R\n")]
    [InlineData("5 0 obj\n(text)\nendobj\n")]
    [InlineData("5 0 obj\n<</Type/XRef>>\nendobj\n")]
    public void AnOffsetThatPointsAtNeitherATableNorAStreamIsRebuilt(string target)
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        int offset = pdf.ToString().Length;

        PdfSource source = Open(pdf.Raw(target).Raw($"trailer\n<</Root 1 0 R>>\nstartxref\n{offset}\n%%EOF\n"));

        Assert.True(source.WasRepaired);
        Assert.Single(source.Pages);
    }

    [Fact]
    public void ACrossReferenceStreamWithNoTypeFieldListsEveryObjectInUse()
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        pdf.StreamSection(4, "/Index[1 3]/Root 1 0 R", [0, 4, 2], InUse(pdf, 1), InUse(pdf, 2), InUse(pdf, 3));

        PdfSource source = Open(pdf);

        Assert.False(source.WasRepaired);
        Assert.Equal([1, 2, 3, 4], source.ObjectNumbers);
        Assert.Single(source.Pages);
    }

    [Fact]
    public void AnEntryOfAnUnknownTypeIsTheNullObject()
    {
        HandmadePdf pdf = HandmadePdf.OnePage().Object(4, "(listed as neither)");
        pdf.StreamSection(5, "/Size 5/Root 1 0 R", [1, 4, 2], [0, 0, 0], InUse(pdf, 1), InUse(pdf, 2), InUse(pdf, 3), [3, pdf.OffsetOf(4), 0]);

        PdfSource source = Open(pdf);

        Assert.Equal(PdfValueKind.Null, Value(source, 4).Kind);
        Assert.False(source.WasRepaired);
    }

    /// <summary>
    /// <paramref name="pdf"/>, listed by a cross-reference stream that says object 4 is kept at <paramref name="index"/>
    /// in object 5.
    /// </summary>
    private static PdfSource InObjectStream(HandmadePdf pdf, int index)
    {
        pdf.StreamSection(6, "/Size 6/Root 1 0 R", [1, 4, 2], [0, 0, 0], InUse(pdf, 1), InUse(pdf, 2), InUse(pdf, 3), [2, 5, index], InUse(pdf, 5));
        return Open(pdf);
    }

    [Theory]
    [InlineData("<</Type/ObjStm/N 2/First 8>>", "6 0 4 7 (other)(packed)", 0)]
    [InlineData("<</Type/ObjStm/N 1/First 4>>", "4 0 (packed)", 5)]
    [InlineData("<</Type/ObjStm/N 2/First 17>>", "4294967300 0 4 8 (wrong!)(packed)", 0)]
    public void AnObjectNotAtTheIndexItsSectionGivesIsFoundByItsNumber(string dictionary, string data, int index)
    {
        // In the third, a number too large for 32 bits must not wrap round to pass for object 4.
        PdfSource source = InObjectStream(HandmadePdf.OnePage().Stream(5, dictionary, data), index);

        Assert.Equal("packed", Text(source, 4));
        Assert.False(source.WasRepaired);
    }

    [Theory]
    [InlineData("<</Type/ObjStm/First 4>>", "4 0 (packed)")]
    [InlineData("<</Type/ObjStm/N/One/First 4>>", "4 0 (packed)")]
    [InlineData("<</Type/ObjStm/N 1/First 4>>", "6 0 (packed)")]
    [InlineData("(not a stream)", null)]
    public void AnObjectStreamThatDoesNotHoldWhatItsSectionSaysTriggersARepair(string dictionary, string? data)
    {
        HandmadePdf pdf = HandmadePdf.OnePage();
        PdfSource source = InObjectStream(data is null ? pdf.Object(5, dictionary) : pdf.Stream(5, dictionary, data), 0);

        Assert.Equal(PdfValueKind.Null, Value(source, 4).Kind);
        Assert.True(source.WasRepaired);
    }

    [Theory]
    [InlineData("<</Type/ObjStm/N 1>>")]
    [InlineData("<</Type/ObjStm/N 1/First/Four>>")]
    public void AnObjectStreamWithoutAFirstOffsetCountsFromTheStartOfItsData(string dictionary)
    {
        PdfSource source = Open(HandmadePdf.OnePage().Stream(5, dictionary, "4 4 (packed)").Raw("trailer\n<</Root 1 0 R>>\n%%EOF\n"));

        Assert.True(source.WasRepaired);
        Assert.Equal("packed", Text(source, 4));
    }

    [Theory]
    [InlineData("<</Type/ObjStm/N 1/First 4/Filter/JBIG2Decode>>")]
    [InlineData("<</Type/ObjStm/N 1/First 4/Filter[7]>>")]
    public void AnObjectStreamThatCannotBeDecodedHoldsNothingWhenRebuilt(string dictionary)
    {
        // A filter that is not a name is damage: it once escaped as an InvalidOperationException and failed the whole file.
        PdfSource source = Open(HandmadePdf.OnePage().Stream(5, dictionary, "4 0 (packed)").Raw("trailer\n<</Root 1 0 R>>\n%%EOF\n"));

        Assert.True(source.WasRepaired);
        Assert.Single(source.Pages);
        Assert.Equal(PdfValueKind.Null, Value(source, 4).Kind);
    }
}
