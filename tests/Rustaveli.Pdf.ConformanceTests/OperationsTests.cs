using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Files put together from the specimens are sound, and a file saved unchanged still meets the standards it claims.
/// </summary>
public class OperationsTests
{
    [Theory]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void AssembledFilesPassQpdfCheck(Specimen specimen)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = specimen.Build().ExportPdf();
        byte[] other = SpecimenCatalog.All[0].Build().ExportPdf();

        byte[] assembled = PdfFile.Open(pdf)
            .Append(PdfFile.Open(other))
            .Overlay(PdfFile.Open(other), onto: "1")
            .Underlay(PdfFile.Open(pdf), onto: "last")
            .KeepPages("last-1")
            .ToArray();

        Assert.Contains("No syntax or stream encoding errors found", Qpdf.Check(assembled), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void FilesOptimizedForTheWebPassQpdfsLinearizationCheck(Specimen specimen)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = specimen.Build().ExportPdf();
        byte[] other = SpecimenCatalog.All[0].Build().ExportPdf();

        byte[] linear = PdfFile.Open(pdf).Append(PdfFile.Open(other)).Append(PdfFile.Open(pdf)).OptimizeForWeb().ToArray();
        byte[] protectedLinear = PdfFile.Open(pdf).Protect(new Protection { UserPassword = "web", Encryption = EncryptionLevel.Rc4With128Bits }).OptimizeForWeb().ToArray();

        foreach ((byte[] file, string password) in new[] { (linear, string.Empty), (protectedLinear, "web") })
        {
            (int code, string report, _) = Qpdf.Run(file, $"--password={password}", "--check-linearization", "{input}");
            Assert.True(code == 0, report);
            Assert.Contains("no linearization errors", report, StringComparison.Ordinal);

            (int checkCode, string check, _) = Qpdf.Run(file, $"--password={password}", "--check", "{input}");
            Assert.True(checkCode == 0, check);
            Assert.Contains("File is linearized", check, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AFileSavedUnchangedStillMeetsItsStandards()
    {
        TestFonts.EnsureRegistered();
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (Specimen specimen in SpecimenCatalog.All)
        {
            Document document = specimen.Build();
            document.Info.Title ??= specimen.Name;
            document.Info.Language ??= "en";

            byte[] exported = document.ExportPdf(new PdfExportOptions
            {
                Conformance = PdfAConformance.PdfA2A,
                Accessibility = PdfUAConformance.PdfUA1,
                ImageProcessor = SkiaImageProcessor.Instance,
            });

            files.Add(specimen.Name, PdfFile.Open(exported).ToArray());
        }

        string[] broken = VeraPdf.Validate(files)
            .SelectMany(file => file.Value.Select(rule => $"{file.Key}: {rule}"))
            .ToArray();

        Assert.True(broken.Length == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }

    /// <summary>The Factur-X description of an invoice attached to a PDF/A-3 file, with the schema PDF/A needs to know it by.</summary>
    internal static readonly string FacturX =
        "<rdf:Description rdf:about=\"\" xmlns:fx=\"urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#\">"
        + "<fx:DocumentType>INVOICE</fx:DocumentType><fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>"
        + "<fx:Version>1.0</fx:Version><fx:ConformanceLevel>EN 16931</fx:ConformanceLevel></rdf:Description>"
        + "<rdf:Description rdf:about=\"\" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\""
        + " xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\" xmlns:pdfaProperty=\"http://www.aiim.org/pdfa/ns/property#\">"
        + "<pdfaExtension:schemas><rdf:Bag><rdf:li rdf:parseType=\"Resource\">"
        + "<pdfaSchema:schema>Factur-X PDFA Extension Schema</pdfaSchema:schema>"
        + "<pdfaSchema:namespaceURI>urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#</pdfaSchema:namespaceURI>"
        + "<pdfaSchema:prefix>fx</pdfaSchema:prefix><pdfaSchema:property><rdf:Seq>"
        + Property("DocumentFileName", "The name of the embedded XML document")
        + Property("DocumentType", "The type of the hybrid document in capital letters")
        + Property("Version", "The actual version of the standard applying to the embedded XML document")
        + Property("ConformanceLevel", "The conformance level of the embedded XML document")
        + "</rdf:Seq></pdfaSchema:property></rdf:li></rdf:Bag></pdfaExtension:schemas></rdf:Description>";

    private static string Property(string name, string description) =>
        "<rdf:li rdf:parseType=\"Resource\">"
        + $"<pdfaProperty:name>{name}</pdfaProperty:name><pdfaProperty:valueType>Text</pdfaProperty:valueType>"
        + $"<pdfaProperty:category>external</pdfaProperty:category><pdfaProperty:description>{description}</pdfaProperty:description>"
        + "</rdf:li>";

    [Fact]
    public void AnInvoiceAttachedToAPdfA3FileKeepsItPdfA3()
    {
        TestFonts.EnsureRegistered();
        byte[] archived = SpecimenCatalog.All[0].Build().ExportPdf(new PdfExportOptions { Conformance = PdfAConformance.PdfA3B });

        byte[] invoice = PdfFile.Open(archived)
            .Attach(new FileAttachment("factur-x.xml", "<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"/>"u8.ToArray())
            {
                MediaType = "text/xml",
                Description = "Factur-X invoice",
                Relationship = AttachmentRelationship.Alternative,
            })
            .AddMetadata(FacturX)
            .ToArray();

        IReadOnlyList<string> broken = VeraPdf.Validate(new Dictionary<string, byte[]> { ["invoice"] = invoice })["invoice"];

        Assert.True(broken.Count == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }
}
