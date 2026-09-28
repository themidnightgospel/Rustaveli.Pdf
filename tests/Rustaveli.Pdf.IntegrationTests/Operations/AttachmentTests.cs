using System.Text;
using System.Text.RegularExpressions;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Writing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests.Operations;

/// <summary>
/// Files attached to a PDF, and descriptions added to its metadata, as a reader finds them.
/// </summary>
public class AttachmentTests
{
    private static byte[] Document(PdfExportOptions? options = null, int pages = 1)
    {
        Document document = Rustaveli.Pdf.Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Stack(stack =>
            {
                for (int page = 1; page <= pages; page++)
                {
                    if (page > 1)
                        stack.Add().NewPage();

                    stack.Add().Text("Page " + page);
                }
            });
        }));

        document.Info.Title = "Invoice";
        return document.ExportPdf(options ?? new PdfExportOptions { Compress = false });
    }

    private static List<(string Name, string Content)> Attached(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        Assert.True(document.Advanced.TryGetEmbeddedFiles(out IReadOnlyList<EmbeddedFile>? files));
        return files!.Select(file => (file.Name, Encoding.UTF8.GetString(file.Bytes.ToArray()))).ToList();
    }

    private static FileAttachment Text(string name, string content, AttachmentRelationship relationship = AttachmentRelationship.Unspecified) =>
        new FileAttachment(name, Encoding.UTF8.GetBytes(content)) { MediaType = "text/plain", Relationship = relationship };

    [Fact]
    public void AttachedFilesAreListedWithTheirContent()
    {
        byte[] pdf = PdfFile.Open(Document())
            .Attach(Text("notes.txt", "Remember"))
            .Attach(new FileAttachment("data.csv", Encoding.UTF8.GetBytes("a,b")) { Description = "The figures" })
            .ToArray();

        Assert.Equal([("data.csv", "a,b"), ("notes.txt", "Remember")], Attached(pdf).OrderBy(file => file.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void AnAttachmentSaysWhatItIsAndHowItRelatesToTheDocument()
    {
        DateTimeOffset created = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        string pdf = Encoding.Latin1.GetString(PdfFile.Open(Document())
            .Attach(new FileAttachment("factur-x.xml", Encoding.UTF8.GetBytes("<Invoice/>"))
            {
                MediaType = "text/xml",
                Description = "The invoice as data",
                Relationship = AttachmentRelationship.Alternative,
                CreationDate = created,
                ModificationDate = created,
            })
            .ToArray());

        byte[] saved = PdfFile.Open(Encoding.Latin1.GetBytes(pdf)).ToArray();

        Assert.Equal([("factur-x.xml", "<Invoice/>")], Attached(saved));

        PdfSource source = PdfSource.Open(saved);
        PdfDictionary specification = source.Resolve(Assert.Single(source.Resolve(source.Catalog[new PdfName("AF")]).AsArray())).AsDictionary();
        PdfDictionary embedded = source.Resolve(source.Resolve(specification[new PdfName("EF")]).AsDictionary()[new PdfName("F")]).AsDictionary();
        PdfDictionary parameters = source.Resolve(embedded[new PdfName("Params")]).AsDictionary();

        Assert.Equal("Alternative", specification[new PdfName("AFRelationship")].AsName().Value);
        Assert.Equal("text/xml", embedded[PdfNames.Subtype].AsName().Value);
        Assert.Equal(10L, parameters[PdfNames.Size].AsInteger());
        Assert.StartsWith("D:20260102030405", Encoding.ASCII.GetString(parameters[PdfNames.ModDate].AsString().Bytes.ToArray()), StringComparison.Ordinal);
        Assert.Equal(16, parameters[new PdfName("CheckSum")].AsString().Bytes.Length);
    }

    [Fact]
    public void AttachmentsAlreadyThereAreKeptWhateverPagesAre()
    {
        byte[] first = PdfFile.Open(Document(pages: 3)).Attach(Text("kept.txt", "Old")).ToArray();

        byte[] again = PdfFile.Open(first).KeepPages("2").Attach(Text("added.txt", "New")).ToArray();

        Assert.Equal([("added.txt", "New"), ("kept.txt", "Old")], Attached(again).OrderBy(file => file.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void AttachmentsOfOneNameAreListedApart()
    {
        byte[] pdf = PdfFile.Open(PdfFile.Open(Document()).Attach(Text("a.txt", "1")).ToArray())
            .Attach(Text("a.txt", "2"))
            .Attach(Text("a.txt", "3"))
            .ToArray();

        Assert.Equal(["a.txt", "a.txt (2)", "a.txt (3)"], Attached(pdf).Select(file => file.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void AnAttachmentIsReadFromAFileByItsName()
    {
        string path = Path.Combine(Path.GetTempPath(), $"attach-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "From disk");

        try
        {
            FileAttachment attachment = FileAttachment.FromFile(path);

            Assert.Equal(Path.GetFileName(path), attachment.Name);
            Assert.NotNull(attachment.ModificationDate);
            Assert.Equal([(Path.GetFileName(path), "From disk")], Attached(PdfFile.Open(Document()).Attach(attachment).ToArray()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnAttachmentNeedsANameAndContent()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FileAttachment(" ", []));
        Assert.Throws<ArgumentNullException>(() => new FileAttachment("a", null!));
        Assert.ThrowsAny<ArgumentException>(() => FileAttachment.FromFile(string.Empty));
        Assert.Throws<ArgumentNullException>(() => PdfFile.Open(Document()).Attach(null!));
        Assert.Equal(AttachmentRelationship.Unspecified, new FileAttachment("a", []).Relationship);
    }

    private const string Invoice =
        "<rdf:Description rdf:about=\"\" xmlns:fx=\"urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#\">"
        + "<fx:DocumentType>INVOICE</fx:DocumentType><fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>"
        + "</rdf:Description>";

    private static string Metadata(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        Assert.True(document.TryGetXmpMetadata(out UglyToad.PdfPig.Content.XmpMetadata? metadata));
        return Encoding.UTF8.GetString(metadata!.GetXmlBytes().ToArray());
    }

    [Fact]
    public void MetadataIsAddedToWhatIsThere()
    {
        byte[] archived = Document(new PdfExportOptions { Conformance = PdfAConformance.PdfA3B });

        string metadata = Metadata(PdfFile.Open(archived).AddMetadata(Invoice).ToArray());

        Assert.Contains("<pdfaid:part>3</pdfaid:part>", metadata, StringComparison.Ordinal);
        Assert.Contains("<fx:DocumentType>INVOICE</fx:DocumentType>", metadata, StringComparison.Ordinal);
        Assert.StartsWith("<?xpacket begin=", metadata, StringComparison.Ordinal);
        Assert.EndsWith("<?xpacket end=\"w\"?>", metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileWithoutMetadataGainsAPacketHoldingWhatIsAdded()
    {
        string metadata = Metadata(PdfFile.Open(Document()).AddMetadata(Invoice).AddMetadata("<rdf:Description rdf:about=\"\"/><rdf:Description rdf:about=\"x\"/>").ToArray());

        Assert.Equal(3, Regex.Matches(metadata, "<rdf:Description").Count);
        Assert.Contains("adobe:ns:meta/", metadata, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<unclosed>")]
    [InlineData("just text")]
    [InlineData("<a:b/>")]
    public void MetadataThatIsNotXmlIsRefused(string xmp) =>
        Assert.Throws<ArgumentException>(() => PdfFile.Open(Document()).AddMetadata(xmp).ToArray());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MetadataMustSaySomething(string? xmp) =>
        Assert.ThrowsAny<ArgumentException>(() => PdfFile.Open(Document()).AddMetadata(xmp!));
}
