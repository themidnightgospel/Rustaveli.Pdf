using System.Globalization;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The example of docs/guide/coming-from-questpdf.md, as written there.</summary>
public class ComingFromQuestPdfTests
{
    [Fact]
    public void AStatementWithARepeatingHeaderAndPageNumbers()
    {
        Document statement = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A4;
            section.Margins = Sides.All(2.Centimetres());
            section.DefaultType = TypeStyle.Default.WithPointSize(10);

            section.RunningHead().InsetBottom(12).Text(text => text.Run("Statement of account").PointSize(16).Bold());

            section.Body().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.Fixed(80);
                    columns.Share();
                    columns.Fixed(80);
                });

                table.HeaderRows(header =>
                {
                    header.Cell().StrokeBottom(1).Text("Date");
                    header.Cell().StrokeBottom(1).Text("Description");
                    header.Cell().StrokeBottom(1).FlushRight().Text("Amount");
                });

                for (int day = 1; day <= 90; day++)
                {
                    table.Cell().InsetVertical(3).Text(new DateTime(2026, 1, 1).AddDays(day - 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    table.Cell().InsetVertical(3).Text("Transfer " + day.ToString(CultureInfo.InvariantCulture));
                    table.Cell().InsetVertical(3).FlushRight().Text((day * 12.5m).ToString("0.00", CultureInfo.InvariantCulture));
                }
            });

            section.RunningFoot().Centered().Text(text =>
            {
                text.Run("Page ");
                text.Folio();
                text.Run(" of ");
                text.PageCount();
            });
        }));

        IReadOnlyList<string> pages = GuideReader.PageTexts(statement.ExportPdf());

        Assert.True(pages.Count > 1);

        for (int page = 0; page < pages.Count; page++)
        {
            Assert.StartsWith("Statement of account Date Description Amount", pages[page], StringComparison.Ordinal);
            Assert.EndsWith($"Page {page + 1} of {pages.Count}", pages[page], StringComparison.Ordinal);
        }

        Assert.Contains("2026-03-31 Transfer 90 1125.00", pages[^1], StringComparison.Ordinal);
    }
}
