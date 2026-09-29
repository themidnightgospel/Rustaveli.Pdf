using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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

    /// <summary>A description of an invoice, as <see cref="Invoice"/>, with the extension schema PDF/A needs to know it by.</summary>
    private static string DescribedInvoice(string type) =>
        $"<rdf:Description rdf:about=\"\" xmlns:fx=\"urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#\"><fx:DocumentType>{type}</fx:DocumentType></rdf:Description>"
        + "<rdf:Description rdf:about=\"\" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\""
        + " xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\" xmlns:pdfaProperty=\"http://www.aiim.org/pdfa/ns/property#\">"
        + "<pdfaExtension:schemas><rdf:Bag><rdf:li rdf:parseType=\"Resource\">"
        + "<pdfaSchema:schema>Factur-X PDFA Extension Schema</pdfaSchema:schema>"
        + "<pdfaSchema:namespaceURI>urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#</pdfaSchema:namespaceURI>"
        + "<pdfaSchema:prefix>fx</pdfaSchema:prefix><pdfaSchema:property><rdf:Seq><rdf:li rdf:parseType=\"Resource\">"
        + "<pdfaProperty:name>DocumentType</pdfaProperty:name><pdfaProperty:valueType>Text</pdfaProperty:valueType>"
        + "<pdfaProperty:category>external</pdfaProperty:category><pdfaProperty:description>The type of the document</pdfaProperty:description>"
        + "</rdf:li></rdf:Seq></pdfaSchema:property></rdf:li></rdf:Bag></pdfaExtension:schemas></rdf:Description>";

    [Fact]
    public void MetadataAddedOverWhatIsThereHoldsEachPropertyOnce()
    {
        Rustaveli.Pdf.Document document = Rustaveli.Pdf.Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Text("Invoice");
        }));
        document.Info.Title = "Invoice";
        document.Info.Language = "en";
        byte[] archived = document.ExportPdf(new PdfExportOptions { Conformance = PdfAConformance.PdfA3B, Accessibility = PdfUAConformance.PdfUA1 });

        byte[] once = PdfFile.Open(archived).AddMetadata(DescribedInvoice("DRAFT")).ToArray();
        XDocument twice = XDocument.Parse(Metadata(PdfFile.Open(once).AddMetadata(DescribedInvoice("INVOICE")).AddMetadata(DescribedInvoice("INVOICE")).ToArray()));

        XNamespace extension = "http://www.aiim.org/pdfa/ns/extension/";
        XNamespace schema = "http://www.aiim.org/pdfa/ns/schema#";
        XNamespace factur = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";
        XElement schemas = Assert.Single(twice.Descendants(extension + "schemas"));

        Assert.Equal(["http://www.aiim.org/pdfua/ns/id/", "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#"], schemas.Descendants(schema + "namespaceURI").Select(uri => uri.Value));
        Assert.Equal("INVOICE", Assert.Single(twice.Descendants(factur + "DocumentType")).Value);
        Assert.Single(twice.Descendants(XNamespace.Get("http://www.aiim.org/pdfa/ns/id/") + "part"));
    }

    [Fact]
    public void APropertyGivenAsAnAttributeIsReplacedLikeAnyOther()
    {
        static string Kind(string rest) => "<rdf:Description rdf:about=\"\" xmlns:k=\"urn:kind\" " + rest + "</rdf:Description>";

        byte[] pdf = PdfFile.Open(Document())
            .AddMetadata(Kind("k:Kind=\"a\"><k:Other>kept</k:Other>"))
            .AddMetadata(Kind("><k:Kind>b</k:Kind>"))
            .AddMetadata(Kind("k:Kind=\"c\">"))
            .AddMetadata("<rdf:Description rdf:about=\"\" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\"><pdfaExtension:schemas/></rdf:Description>")
            .AddMetadata(DescribedInvoice("INVOICE"))
            .ToArray();

        XDocument metadata = XDocument.Parse(Metadata(pdf));
        XNamespace kind = "urn:kind";
        List<XElement> descriptions = metadata.Descendants(XNamespace.Get("http://www.w3.org/1999/02/22-rdf-syntax-ns#") + "Description").ToList();

        Assert.Equal(["c"], descriptions.Select(description => (string?)description.Attribute(kind + "Kind")).OfType<string>());
        Assert.Empty(metadata.Descendants(kind + "Kind"));
        Assert.Equal("kept", Assert.Single(metadata.Descendants(kind + "Other")).Value);
        Assert.Single(metadata.Descendants(XNamespace.Get("http://www.aiim.org/pdfa/ns/schema#") + "namespaceURI"));
        Assert.Contains("<pdfaSchema:namespaceURI>", Metadata(pdf), StringComparison.Ordinal);
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
