using System.Globalization;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The examples of docs/guide/images-and-artwork.md, as written there.</summary>
[Collection(WorkingDirectoryCollection.Name)]
public class ImagesAndArtworkTests
{
    [Fact]
    public void ImagesAndTheirQuality()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "assets", "images", "jpeg-baseline.jpg"), "photo.jpg");

        RasterImage photo = RasterImage.FromFile("photo.jpg");

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(36);

            section.Body().Stack(stack =>
            {
                stack.SpaceBetween(12);
                stack.Add().Image(photo);
                stack.Add().Columns(columns =>
                {
                    columns.Gutter(8);
                    columns.Share().Height(80).Image(photo, ImageFitting.Proportionally);
                    columns.Share().Height(80).Image(photo, ImageFitting.Stretch);
                });
            });
        }));

        byte[] pdf = document.ExportPdf(new PdfExportOptions
        {
            MaximumImageResolution = 150,
            ImageQuality = 80,
            ImageProcessor = SkiaImageProcessor.Instance,
        });

        using UglyToad.PdfPig.PdfDocument read = UglyToad.PdfPig.PdfDocument.Open(pdf);
        List<UglyToad.PdfPig.Content.IPdfImage> images = read.GetPage(1).GetImages().ToList();
        Assert.Equal(3, images.Count);

        // One image placed three times is embedded once.
        Assert.Single(images.Select(image => image.RawBytes.Length).Distinct());
        Assert.True(document.ExportPdf().Length > 0);
    }

    [Fact]
    public void ArtworkFromSvg()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "assets", "svg", "specimen.svg"), "logo.svg");

        Document document = Page(section =>
        {
            Artwork logo = Artwork.FromSvgFile("logo.svg");

            section.Body().Width(120).Artwork(logo);
        });

        using UglyToad.PdfPig.PdfDocument read = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        Assert.Empty(read.GetPage(1).GetImages());
        Assert.NotEmpty(read.GetPage(1).Paths);
    }

    [Fact]
    public void DrawingArtwork()
    {
        Document document = Page(section =>
        {
            Artwork badge = Artwork.Draw(120, 40, draw =>
            {
                VectorPath outline = new VectorPath().AddRoundedRectangle(0, 0, 120, 40, Corners.All(20));
                draw.Fill(outline, Gradient.Across(Ink.Hex("#1E88E5"), Ink.Hex("#8E24AA")));
                draw.Stroke(outline, Ink.Hex("#0D47A1"), new LineStyle(1.5f));
                draw.Text("Approved", 60, 25, TypeStyle.Default.WithPointSize(14).Bold().WithInk(Ink.White), TextAnchor.Middle);
            });

            section.Body().Width(120).Artwork(badge);
        });

        Assert.Equal("Approved", GuideReader.Text(document.ExportPdf()));
    }

    [Fact]
    public void MadeForTheirBox()
    {
        // In a culture that writes a decimal comma, as the example must still be right in.
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            AssertMadeForTheirBox();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    private static void AssertMadeForTheirBox()
    {
        Document document = Page(section =>
        {
            section.Body().Height(160).Artwork(size => Artwork.FromSvg(FormattableString.Invariant(
                $"""
                <svg xmlns="http://www.w3.org/2000/svg" width="{size.Width}" height="{size.Height}">
                  <rect x="0" y="{size.Height * 0.4}" width="{size.Width / 3}" height="{size.Height * 0.6}" fill="#43A047"/>
                  <rect x="{size.Width / 3}" y="{size.Height * 0.1}" width="{size.Width / 3}" height="{size.Height * 0.9}" fill="#1E88E5"/>
                  <rect x="{size.Width * 2 / 3}" y="{size.Height * 0.7}" width="{size.Width / 3}" height="{size.Height * 0.3}" fill="#FB8C00"/>
                </svg>
                """)));
        });

        using UglyToad.PdfPig.PdfDocument read = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        List<(double R, double G, double B)> fills = read.GetPage(1).Paths
            .Where(path => path.IsFilled && path.FillColor is not null)
            .Select(path => path.FillColor!.ToRGBValues())
            .Select(rgb => (Math.Round(rgb.r * 255), Math.Round(rgb.g * 255), Math.Round(rgb.b * 255)))
            .ToList();

        Assert.Contains((0x43, 0xA0, 0x47), fills);
        Assert.Contains((0x1E, 0x88, 0xE5), fills);
        Assert.Contains((0xFB, 0x8C, 0x00), fills);

        // The three bars side by side fill the width between the margins, each a third of it.
        List<double> lefts = read.GetPage(1).Paths
            .Where(path => path.IsFilled && path.FillColor is not null)
            .Where(path => path.FillColor!.ToRGBValues() is var rgb && Math.Round(rgb.r * 255) is 0x43 or 0x1E or 0xFB)
            .Select(path => path.GetBoundingRectangle()!.Value.Left)
            .OrderBy(left => left)
            .ToList();
        double width = PaperSizes.A5.Width - 72;
        double[] expected = [36, 36 + (width / 3), 36 + (width * 2 / 3)];
        Assert.Equal(3, lefts.Count);
        Assert.All(Enumerable.Range(0, 3), bar => Assert.True(Math.Abs(lefts[bar] - expected[bar]) < 0.1, $"Bar {bar + 1} starts at {lefts[bar]}, not {expected[bar]}."));
    }

    /// <summary>An A5 page, as the examples that begin at the body are set on.</summary>
    private static Document Page(Action<Section> compose) => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = PaperSizes.A5;
        section.Margins = Sides.All(36);
        compose(section);
    }));
}
