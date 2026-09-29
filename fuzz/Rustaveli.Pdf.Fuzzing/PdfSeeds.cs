namespace Rustaveli.Pdf.Fuzzing;

/// <summary>
/// The PDF files the pdf target starts from, made with this library: several pages, links and bookmarks, PDF/A with
/// an attachment, tagged PDF, a protected file and a linearised one, so that mutation begins from every kind of
/// structure the reader handles.
/// </summary>
internal static class PdfSeeds
{
    public static void Write(string folder)
    {
        Directory.CreateDirectory(folder);

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A6;
            section.RunningFoot().Text(text =>
            {
                text.Run("Page ");
                text.Folio();
                text.Run(" of ");
                text.PageCount();
            });

            section.Body().Stack(stack =>
            {
                stack.Add().Bookmark("Start").Anchor("start").Text("Seed document");
                stack.Add().Link("https://example.com").Text("A link");
                stack.Add().Table(table =>
                {
                    table.Columns(columns =>
                    {
                        columns.Share();
                        columns.Share();
                    });

                    for (int row = 0; row < 40; row++)
                    {
                        table.Cell().Text($"Row {row}");
                        table.Cell().Fill(Ink.Hex("#1565C0")).Text(text => text.Run("value").Ink(Ink.White));
                    }
                });
            });
        }));

        // PDF/UA asks for both.
        document.Info.Title = "Seed document";
        document.Info.Language = "en";

        byte[] plain = document.ExportPdf();
        File.WriteAllBytes(Path.Combine(folder, "plain.pdf"), plain);
        File.WriteAllBytes(Path.Combine(folder, "tagged.pdf"), document.ExportPdf(new PdfExportOptions { Accessibility = PdfUAConformance.PdfUA1 }));
        File.WriteAllBytes(Path.Combine(folder, "protected.pdf"), document.ExportPdf(new PdfExportOptions { Protection = new Protection { OwnerPassword = "owner" } }));
        File.WriteAllBytes(Path.Combine(folder, "linearised.pdf"), PdfFile.Open(plain).OptimizeForWeb().ToArray());
        File.WriteAllBytes(
            Path.Combine(folder, "pdfa3-attachment.pdf"),
            PdfFile.Open(document.ExportPdf(new PdfExportOptions { Conformance = PdfAConformance.PdfA3B }))
                .Attach(new FileAttachment("data.xml", "<data/>"u8.ToArray()))
                .ToArray());
    }
}
