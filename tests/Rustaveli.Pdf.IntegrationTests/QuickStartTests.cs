using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// The README's quick start, verbatim apart from writing to a temporary file, so the first code a reader sees
/// always compiles and works. Change both together.
/// </summary>
public class QuickStartTests
{
    [Fact]
    public void TheReadmeExampleExportsAStatement()
    {
        string path = Path.Combine(Path.GetTempPath(), $"statement-{Guid.NewGuid():N}.pdf");
        try
        {
            (string Code, string Description, string Amount)[] rows =
            [
                ("A-100", "Consulting", "1,200.00"),
                ("B-200", "Licences", "300.00"),
            ];

            Document document = Document.Compose(composition => composition.Section(section =>
            {
                section.Trim = PaperSizes.A4;
                section.Margins = Sides.All(40);
                section.DefaultType = TypeStyle.Default.WithTypeface("Noto Sans").WithPointSize(11);

                section.RunningHead().Text("Quarterly Statement");

                section.RunningFoot().Centered().Text(text =>
                {
                    text.Run("Page ");
                    text.Folio();
                    text.Run(" of ");
                    text.PageCount();
                });

                section.Body().Table(table =>
                {
                    table.Columns(columns =>
                    {
                        columns.Fixed(90);
                        columns.Share();
                        columns.Fixed(70);
                    });

                    table.HeaderRows(header =>
                    {
                        header.Cell().Text("Code");
                        header.Cell().Text("Description");
                        header.Cell().FlushRight().Text("Amount");
                    });

                    foreach ((string code, string description, string amount) in rows)
                    {
                        table.Cell().Text(code);
                        table.Cell().Text(description);
                        table.Cell().FlushRight().Text(amount);
                    }
                });
            }));

            document.ExportPdf(path);

            using PdfDocument pdf = PdfDocument.Open(path);
            string words = string.Join(" ", pdf.GetPage(1).GetWords().Select(word => word.Text));
            Assert.Equal(1, pdf.NumberOfPages);
            Assert.Contains("Quarterly Statement", words, StringComparison.Ordinal);
            Assert.Contains("Consulting", words, StringComparison.Ordinal);
            Assert.Contains("Page 1 of 1", words, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
