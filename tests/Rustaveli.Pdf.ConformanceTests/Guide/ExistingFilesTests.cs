using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The examples of docs/guide/existing-files.md, as written there, over files made for them.</summary>
[Collection(WorkingDirectoryCollection.Name)]
public class ExistingFilesTests
{
    [Fact]
    public void CombiningDocumentsWhileComposing()
    {
        Document cover = Document.Compose(composition => composition.Section(section => section.Body().Text("Cover")));
        Document report = Document.Compose(composition => composition.Section(section => section.Body().Text("Report")));

        Document merged = Document.Merge(cover, report);
        Document numberedApart = Document.Merge(cover, report).NumberPartsSeparately();

        Assert.Equal(["Cover", "Report"], GuideReader.PageTexts(merged.ExportPdf()));
        Assert.Equal(2, GuideReader.PageTexts(numberedApart.ExportPdf()).Count);
    }

    [Fact]
    public void PuttingFilesTogether()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        Pages("contract.pdf", "Contract", 10);
        Pages("appendix.pdf", "Appendix", 1);
        Pages("terms.pdf", "Terms", 3);

        PdfFile.Open("contract.pdf")
            .KeepPages("1-3, 5, 8-last")
            .Append("appendix.pdf")
            .Append("terms.pdf", pages: "2")
            .Save("contract-with-appendix.pdf");

        Assert.Equal(
            ["Contract 1", "Contract 2", "Contract 3", "Contract 5", "Contract 8", "Contract 9", "Contract 10", "Appendix 1", "Terms 2"],
            GuideReader.PageTexts("contract-with-appendix.pdf"));
    }

    [Fact]
    public void StampsAndLetterheads()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        Pages("letter.pdf", "Letter", 2);
        Placed("letterhead.pdf", "Letterhead", frame => frame.FlushBottom());
        Placed("copy-stamp.pdf", "Copy", frame => frame.Middle());

        PdfFile.Open("letter.pdf")
            .Underlay("letterhead.pdf", onto: "1")
            .Overlay("copy-stamp.pdf")
            .Save("letter-on-letterhead.pdf");

        IReadOnlyList<string> pages = GuideReader.PageTexts("letter-on-letterhead.pdf");
        Assert.Equal(2, pages.Count);
        Assert.Equal("Letter 1 Copy Letterhead", pages[0]);
        Assert.Equal("Letter 2 Copy", pages[1]);
    }

    [Fact]
    public void AttachmentsAndElectronicInvoices()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        Document invoiceDocument = Document.Compose(composition => composition.Section(section => section.Body().Text("Invoice")));
        invoiceDocument.Info.Title = "Invoice";
        invoiceDocument.ExportPdf("invoice.pdf", new PdfExportOptions { Conformance = PdfAConformance.PdfA3B });
        File.WriteAllText("factur-x.xml", "<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"/>");
        File.WriteAllText("factur-x-metadata.xmp", OperationsTests.FacturX);

        PdfFile.Open("invoice.pdf")
            .Attach(new FileAttachment("factur-x.xml", File.ReadAllBytes("factur-x.xml"))
            {
                MediaType = "text/xml",
                Description = "Factur-X invoice",
                Relationship = AttachmentRelationship.Alternative,
            })
            .AddMetadata(File.ReadAllText("factur-x-metadata.xmp"))
            .Save("invoice-with-data.pdf");

        IReadOnlyList<string> broken = VeraPdf.Validate(new Dictionary<string, byte[]> { ["invoice"] = File.ReadAllBytes("invoice-with-data.pdf") })["invoice"];
        Assert.True(broken.Count == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }

    [Fact]
    public void ProtectionAndWebViewing()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        Pages("report.pdf", "Report", 2);

        PdfFile.Open("report.pdf")
            .Protect(new Protection { UserPassword = "open sesame", AllowPrinting = false })
            .Save("report-protected.pdf");

        PdfFile.Open("report-protected.pdf", password: "open sesame")
            .Unprotect()
            .OptimizeForWeb()
            .Save("report-for-the-web.pdf");

        Assert.Throws<IncorrectPasswordException>(() => PdfFile.Open("report-protected.pdf"));
        Assert.False(PdfFile.Open("report-for-the-web.pdf").WasProtected);

        (int code, string report, _) = Qpdf.Run(File.ReadAllBytes("report-for-the-web.pdf"), "--check-linearization", "{input}");
        Assert.True(code == 0, report);
    }

    /// <summary>Writes a one-page file saying <paramref name="text"/> where <paramref name="place"/> puts it.</summary>
    private static void Placed(string path, string text, Func<IFrame, IFrame> place) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A6;
            place(section.Body()).Text(text);
        })).ExportPdf(path);

    /// <summary>Writes a file of <paramref name="count"/> pages, each saying its name and number.</summary>
    private static void Pages(string path, string name, int count) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A6;
            section.Body().Stack(stack =>
            {
                for (int page = 1; page <= count; page++)
                {
                    if (page > 1)
                        stack.Add().NewPage();

                    stack.Add().Text(name + " " + page.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            });
        })).ExportPdf(path);
}
