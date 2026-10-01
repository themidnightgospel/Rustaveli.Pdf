namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>
/// The home page's invoice (docs/README.md), as the README's quick start writes it, and the frames the preview's
/// inspector sees on its page, which the home page's inspector shows.
/// </summary>
[Collection(WorkingDirectoryCollection.Name)]
public class HomePageTests
{
    [Fact]
    public void TheInvoiceAndTheFramesTheInspectorSees()
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

        GuideOutput.Show(invoice, "invoice");
        IReadOnlyList<GuideOutput.InspectedFrame> frames = GuideOutput.Inspect(invoice, "invoice", PaperSizes.A5);

        Assert.Equal("Invoice INV-0042 Issued 29 September 2026, due in 30 days Item Qty Amount Design review 4 1,600.00 Implementation 12 7,200.00 Support, one month 1 450.00 Total due 9,250.00", GuideReader.Text("invoice.pdf"));
        Assert.Contains(frames, frame => frame.Name == "Table");
        // The title, three header cells, nine body cells and the total.
        Assert.Equal(14, frames.Count(frame => frame.Name == "Text"));
        Assert.All(frames, frame => Assert.InRange(frame.X + frame.Width, 0, 1.0001));
    }
}
