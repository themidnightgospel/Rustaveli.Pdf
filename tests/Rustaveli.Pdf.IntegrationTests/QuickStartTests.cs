using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// The README's quick start, verbatim, run in a directory of its own, so the first code a reader sees always
/// compiles and works. <c>GuideTests</c> fails when the two differ.
/// </summary>
[Collection(WorkingDirectoryCollection.Name)]
public class QuickStartTests
{
    [Fact]
    public void TheReadmeExampleExportsAnInvoice()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();

        (string Item, string Quantity, string Amount)[] lines =
        [
            ("Design review", "4", "1,600.00"),
            ("Implementation", "12", "7,200.00"),
            ("Support, one month", "1", "450.00"),
        ];

        Ink blue = Ink.Hex("#1565C0");

        Document invoice = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(40);
            section.DefaultType = TypeStyle.Default.WithPointSize(10);

            section.Body().Stack(stack =>
            {
                stack.SpaceBetween(20);

                stack.Add().Text(text =>
                {
                    text.Line("Invoice INV-0042").PointSize(22).Bold().Ink(blue);
                    text.Line("Issued 29 September 2026, due in 30 days");
                });

                stack.Add().Table(table =>
                {
                    table.Columns(columns =>
                    {
                        columns.Share();
                        columns.Fixed(40);
                        columns.Fixed(70);
                    });

                    table.HeaderRows(header =>
                    {
                        header.Cell().Fill(blue).Inset(6).Text(text => text.Run("Item").Bold().Ink(Ink.White));
                        header.Cell().Fill(blue).Inset(6).FlushRight().Text(text => text.Run("Qty").Bold().Ink(Ink.White));
                        header.Cell().Fill(blue).Inset(6).FlushRight().Text(text => text.Run("Amount").Bold().Ink(Ink.White));
                    });

                    foreach ((string item, string quantity, string amount) in lines)
                    {
                        table.Cell().StrokeBottom(0.5f).Inset(6).Text(item);
                        table.Cell().StrokeBottom(0.5f).Inset(6).FlushRight().Text(quantity);
                        table.Cell().StrokeBottom(0.5f).Inset(6).FlushRight().Text(amount);
                    }
                });

                stack.Add().FlushRight().Text(text => text.Run("Total due 9,250.00").PointSize(14).Bold());
            });
        }));

        invoice.ExportPdf("invoice.pdf");

        using PdfDocument pdf = PdfDocument.Open(Path.Combine(directory.Location, "invoice.pdf"));
        string words = string.Join(" ", pdf.GetPage(1).GetWords().Select(word => word.Text));
        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Contains("Invoice INV-0042", words, StringComparison.Ordinal);
        Assert.Contains("Support, one month", words, StringComparison.Ordinal);
        Assert.Contains("Total due 9,250.00", words, StringComparison.Ordinal);
    }
}
