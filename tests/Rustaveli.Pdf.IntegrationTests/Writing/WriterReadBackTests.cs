using Rustaveli.Pdf.Writing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Outline.Destinations;

namespace Rustaveli.Pdf.IntegrationTests.Writing;

/// <summary>
/// Reads files from the managed writer back with PdfPig, an independent parser, so that what the writer means to
/// say — pages and their sizes, links and where they lead, document information — is checked by code that shares
/// nothing with it.
/// </summary>
public class WriterReadBackTests
{
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 9, 26, 10, 30, 15, TimeSpan.FromHours(4));

    private static readonly DateTimeOffset Modified = new DateTimeOffset(2026, 9, 27, 8, 5, 0, TimeSpan.FromMinutes(-210));

    private static byte[] Write(string format, Action<PdfDocumentWriter> build)
    {
        PdfWriterOptions options = new PdfWriterOptions
        {
            CrossReferenceFormat = (PdfCrossReferenceFormat)Enum.Parse(typeof(PdfCrossReferenceFormat), format),
        };

        using MemoryStream output = new MemoryStream();
        using (PdfDocumentWriter document = new PdfDocumentWriter(output, options))
        {
            build(document);
            document.Finish();
        }

        return output.ToArray();
    }

    private static byte[] Sample(string format) => Write(format, document =>
    {
        document.Info.Title = "ვეფხისტყაოსანი — “Knight”";
        document.Info.Author = "Shota Rustaveli";
        document.Info.Subject = "Read-back";
        document.Info.Keywords = "pdf; writer";
        document.Info.Creator = "Integration tests";
        document.Info.Producer = "Rustaveli.Pdf";
        document.Info.CreationDate = Created;
        document.Info.ModificationDate = Modified;

        PdfPage first = document.BeginPage(595.28, 841.89);
        first.Content.SetFillRgb(0.2, 0.4, 0.8);
        first.Content.Rectangle(50, 50, 200, 100);
        first.Content.Fill();
        first.AddUriLink(new PdfRectangle(50, 700, 250, 720), "https://example.com/ვ?q=a b");
        first.AddDestinationLink(new PdfRectangle(50, 650, 250, 670), "appendix");
        document.EndPage(first);

        PdfPage second = document.BeginPage(612, 792);
        second.Content.SetStrokeGray(0);
        second.Content.MoveTo(0, 0);
        second.Content.LineTo(612, 792);
        second.Content.Stroke();
        document.EndPage(second);

        PdfPage third = document.BeginPage(400, 300);
        document.AddNamedDestination("appendix", third.Reference, 20, 280);
        document.EndPage(third);
    });

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void ReportsEveryPageWithItsSize(string format)
    {
        using PdfDocument document = PdfDocument.Open(Sample(format));

        Assert.Equal(3, document.NumberOfPages);
        Assert.Equal((595.28, 841.89), (document.GetPage(1).Width, document.GetPage(1).Height));
        Assert.Equal((612.0, 792.0), (document.GetPage(2).Width, document.GetPage(2).Height));
        Assert.Equal((400.0, 300.0), (document.GetPage(3).Width, document.GetPage(3).Height));
    }

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void ReadsTheLinksBackWithTheirTargets(string format)
    {
        using PdfDocument document = PdfDocument.Open(Sample(format));

        List<Annotation> links = document.GetPage(1).GetAnnotations().ToList();

        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal(AnnotationType.Link, link.Type));
        Assert.All(links, link => Assert.Equal(0, link.Border.BorderWidth));

        Annotation web = links[0];
        Assert.Equal((50.0, 700.0, 250.0, 720.0), (web.Rectangle.Left, web.Rectangle.Bottom, web.Rectangle.Right, web.Rectangle.Top));
        Assert.Equal("https://example.com/%E1%83%95?q=a%20b", Assert.IsType<UriAction>(web.Action).Uri);

        ExplicitDestination destination = Assert.IsType<GoToAction>(links[1].Action).Destination;
        Assert.Equal(3, destination.PageNumber);
        Assert.Equal(ExplicitDestinationType.XyzCoordinates, destination.Type);
        Assert.Equal(20, destination.Coordinates.Left);
        Assert.Equal(280, destination.Coordinates.Top);
        Assert.Empty(document.GetPage(2).GetAnnotations());
    }

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void ReadsTheDocumentInformationBack(string format)
    {
        using PdfDocument document = PdfDocument.Open(Sample(format));
        DocumentInformation information = document.Information;

        Assert.Equal("ვეფხისტყაოსანი — “Knight”", information.Title);
        Assert.Equal("Shota Rustaveli", information.Author);
        Assert.Equal("Read-back", information.Subject);
        Assert.Equal("pdf; writer", information.Keywords);
        Assert.Equal("Integration tests", information.Creator);
        Assert.Equal("Rustaveli.Pdf", information.Producer);
        Assert.Equal(Created, information.GetCreatedDateTimeOffset());
        Assert.Equal(Created.Offset, information.GetCreatedDateTimeOffset()!.Value.Offset);
        Assert.Equal(Modified, information.GetModifiedDateTimeOffset());
    }

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void FindsEveryPageOfALongDocument(string format)
    {
        const int PageCount = 1500;
        byte[] pdf = Write(format, document =>
        {
            for (int index = 0; index < PageCount; index++)
            {
                PdfPage page = document.BeginPage(200 + (index % 7), 300);
                page.AddDestinationLink(new PdfRectangle(0, 0, 10, 10), $"page-{PageCount - 1 - index}");
                document.AddNamedDestination($"page-{index}", page.Reference, 0, 300);
                document.EndPage(page);
            }
        });

        using PdfDocument document = PdfDocument.Open(pdf);

        Assert.Equal(PageCount, document.NumberOfPages);
        foreach (int number in new[] { 1, 33, 1024, 1025, 1234, PageCount })
        {
            Page page = document.GetPage(number);
            Assert.Equal(200 + ((number - 1) % 7), page.Width);

            GoToAction action = Assert.IsType<GoToAction>(Assert.Single(page.GetAnnotations()).Action);
            Assert.Equal(PageCount + 1 - number, action.Destination.PageNumber);
        }
    }
}
