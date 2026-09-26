using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using Rustaveli.Pdf.Text;
using SkiaSharp;

namespace Rustaveli.Pdf.ConformanceTests.Specimens;

/// <summary>
/// The conformance corpus: documents that together exercise every feature the engine has. Each one is validated
/// structurally and compared visually, so a new feature earns a place here as soon as it lands.
/// </summary>
public static class SpecimenCatalog
{
    public static IReadOnlyList<Specimen> All { get; } =
    [
        new Specimen("gallery", Gallery),
        new Specimen("transforms", Transforms),
        new Specimen("images", Images),
        new Specimen("layers-and-decoration", LayersAndDecoration),
        new Specimen("long-flow", LongFlow)
    ];

    public static TheoryData<Specimen> Cases()
    {
        TheoryData<Specimen> data = new TheoryData<Specimen>();
        foreach (Specimen specimen in All)
            data.Add(specimen);
        return data;
    }

    private static Document Page(Action<IContainer> content, Action<PageDescriptor>? configure = null) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = PageSizes.A4;
            page.Margin = Edges.All(40f);
            page.DefaultTextStyle = TextStyle.Default.FontFamilyOf(TestFonts.Sans).FontSizeOf(10f);
            configure?.Invoke(page);
            content(page.Content());
        }));

    private static Document Gallery() => Page(content => content.Column(column =>
    {
        column.Spacing(14f);

        column.Item().Text(text =>
        {
            text.Span("Styles compose: ");
            text.Span("bold").Bold();
            text.Span(", ");
            text.Span("italic").Italic();
            text.Span(", ");
            text.Span("underlined").Underline();
            text.Span(", ");
            text.Span("struck through").Strikethrough();
            text.Span(", ");
            text.Span("highlighted").BackgroundColor(TestInks.Yellow);
            text.Span(" and coloured").FontColor(TestInks.Red);
            text.Span(". H");
            text.Span("2").Subscript();
            text.Span("O and e");
            text.Span("iπ").Superscript();
            text.Span(".");
        });

        column.Item().Text(text =>
        {
            text.FirstLineIndent(18f);
            text.Span("A paragraph long enough to wrap across several lines, with a first-line indent, so that line "
                + "breaking, indentation and the measured width of every word all show up in the rendered page.");
        });

        column.Item().Text(text =>
        {
            text.AlignCenter();
            text.Span("Centred").FontColor(TestInks.Teal);
        });

        column.Item().Text(text =>
        {
            text.AlignRight();
            text.Span("Right aligned").FontColor(TestInks.Teal);
        });

        column.Item().Row(row =>
        {
            row.Spacing(10f);
            row.RelativeItem().Border(1f).BorderColor(TestInks.Grey).Padding(8f).Text("Bordered box");
            row.RelativeItem().Height(60f).Background(TestInks.Indigo).Padding(8f).AlignCenter().AlignMiddle()
                .Text(text => text.Span("Centred on both axes").FontColor(TestInks.White));
            row.ConstantItem(60f).Height(60f).Placeholder(TestInks.GreyLighten3);
        });

        column.Item().Row(row =>
        {
            row.Spacing(10f);
            row.RelativeItem().Background(TestInks.AmberLighten4).CornerRadius(8f).Padding(8f).Text("Rounded fill");
            row.RelativeItem().Border(1f).BorderColor(TestInks.Teal).CornerRadius(8f).Padding(8f).Text("Rounded outline");
            row.AutoItem().Background(TestInks.GreyLighten4).Padding(8f).Text("Auto");
        });

        column.Item().Row(row =>
        {
            row.Spacing(14f);
            row.RelativeItem().List(list =>
            {
                list.Item().Text("A bulleted item");
                list.Item().Text("Another, long enough to wrap so continuation lines clear the marker");
            });
            row.RelativeItem().List(list =>
            {
                list.Ordered();
                list.Item().Text("Numbered");
                list.Item().Text("items");
            });
        });

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(70f);
                columns.RelativeColumn();
                columns.ConstantColumn(70f);
            });
            table.Header(header =>
            {
                header.Cell().Background(TestInks.Indigo).Padding(4f).Text(text => text.Span("Code").Bold().FontColor(TestInks.White));
                header.Cell().Background(TestInks.Indigo).Padding(4f).Text(text => text.Span("Description").Bold().FontColor(TestInks.White));
                header.Cell().Background(TestInks.Indigo).Padding(4f).AlignRight().Text(text => text.Span("Amount").Bold().FontColor(TestInks.White));
            });
            for (int index = 1; index <= 6; index++)
            {
                Ink shade = index % 2 == 0 ? TestInks.White : TestInks.GreyLighten4;
                table.Cell().Background(shade).Padding(4f).Text($"SKU-{index:D3}");
                table.Cell().Background(shade).Padding(4f).Text($"Line item {index}");
                table.Cell().Background(shade).Padding(4f).AlignRight().Text($"{index * 12.5m:F2}");
            }
            table.Cell().ColumnSpan(2).Padding(4f).AlignRight().Text(text => text.Span("Total").Bold());
            table.Cell().Padding(4f).AlignRight().Text(text => text.Span("262.50").Bold());
        });

        column.Item().RightToLeft().Row(row =>
        {
            row.Spacing(6f);
            row.ConstantItem(60f).Background(TestInks.Teal).Padding(4f).Text("first");
            row.ConstantItem(60f).Background(TestInks.Cyan).Padding(4f).Text("second");
            row.RelativeItem().Text("right to left");
        });

        column.Item().Section("destination").Text(text =>
        {
            text.Span("A named destination with an external ");
            text.Hyperlink("hyperlink", "https://example.com").FontColor(TestInks.Blue).Underline();
            text.Span(" and an internal ");
            text.SectionLink("jump", "destination").FontColor(TestInks.Blue).Underline();
            text.Span(".");
        });
    }));

    private static Document Transforms() => Page(content => content.Column(column =>
    {
        column.Spacing(16f);

        column.Item().Row(row =>
        {
            row.Spacing(12f);
            row.ConstantItem(80f).Height(40f).RotateLeft().Background(TestInks.TealLighten3).Text("left");
            row.ConstantItem(80f).Height(40f).RotateRight().Background(TestInks.TealLighten3).Text("right");
            row.ConstantItem(80f).FlipHorizontal().Background(TestInks.AmberLighten3).Text("flipped");
            row.ConstantItem(80f).FlipVertical().Background(TestInks.AmberLighten3).Text("flipped");
            row.ConstantItem(80f).FlipOver().Background(TestInks.AmberLighten3).Text("over");
        });

        column.Item().Row(row =>
        {
            row.Spacing(12f);
            row.ConstantItem(120f).Scale(0.5f).Background(TestInks.PinkLighten3).Padding(6f).Text("half size");
            row.ConstantItem(120f).Scale(1.5f, 1f).Background(TestInks.PinkLighten3).Padding(6f).Text("stretched");
            row.ConstantItem(120f).TranslateX(10f).TranslateY(6f).Background(TestInks.PinkLighten3).Padding(6f).Text("moved");
        });

        column.Item().Width(160f).Height(24f).ScaleToFit().Text("Scaled down until this whole sentence fits the box it was given.");

        column.Item().Width(120f).AspectRatio(2f).Background(TestInks.LimeLighten3).AlignCenter().AlignMiddle().Text("2 : 1");
    }));

    private static Document Images() => Page(content => content.Column(column =>
    {
        column.Spacing(10f);

        byte[] photograph = Photograph();

        // Each fit mode gets a box it can satisfy: fitting the width needs free height, and fitting the height needs
        // free width. Area and unproportional fit any box.
        column.Item().Text("Width");
        column.Item().Width(200f).Border(0.5f).BorderColor(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFit.Width);
        column.Item().Text("Height");
        column.Item().Height(100f).AlignLeft().Border(0.5f).BorderColor(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFit.Height);
        column.Item().Text("Area");
        column.Item().Width(300f).Height(120f).Border(0.5f).BorderColor(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFit.Area);
        column.Item().Text("Unproportional");
        column.Item().Width(300f).Height(120f).Border(0.5f).BorderColor(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFit.Unproportional);
    }));

    private static Document LayersAndDecoration() => Page(content => content.Column(column =>
    {
        column.Spacing(16f);

        column.Item().Height(160f).Layers(layers =>
        {
            layers.Layer().AlignCenter().AlignMiddle().Text(text => text.Span("WATERMARK").FontSize(40f).FontColor(TestInks.GreyLighten3));
            layers.PrimaryLayer().Padding(10f).Text("The primary layer sets the size; other layers draw behind or in front of it.");
            layers.Layer().AlignRight().AlignBottom().Padding(6f).Text(text => text.Span("corner").FontColor(TestInks.Red));
        });

        column.Item().Decoration(decoration =>
        {
            decoration.Before().Background(TestInks.IndigoLighten4).Padding(6f).Text("Before");
            decoration.Content().Padding(6f).Text("Decorated content sits between the bands.");
            decoration.After().Background(TestInks.IndigoLighten4).Padding(6f).Text("After");
        });

        column.Item().LineHorizontal(1f, TestInks.Grey);

        column.Item().Height(60f).Row(row =>
        {
            row.RelativeItem().Text("left of the rule");
            row.ConstantItem(20f).AlignCenter().LineVertical(1f, TestInks.Grey);
            row.RelativeItem().Text("right of the rule");
        });
    }));

    private static Document LongFlow() => Document.Create(container => container.Page(page =>
    {
        page.Size = PageSizes.A5;
        page.Margin = Edges.All(30f);
        page.DefaultTextStyle = TextStyle.Default.FontFamilyOf(TestFonts.Sans).FontSizeOf(9f);

        page.Header().PaddingBottom(6f).Text(text => text.Span("Running head").Bold());
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });

        page.Content().Column(column =>
        {
            column.Spacing(6f);

            for (int index = 0; index < 8; index++)
            {
                column.Item().Text($"Paragraph {index + 1}. The quick brown fox jumps over the lazy dog, and then keeps "
                    + "running across the page so that the paragraph wraps onto several lines and the flow reaches the "
                    + "bottom margin, forcing content onto the following page.");
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60f);
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    header.Cell().Background(TestInks.GreyLighten3).Padding(3f).Text("Code");
                    header.Cell().Background(TestInks.GreyLighten3).Padding(3f).Text("Description");
                });
                for (int index = 1; index <= 40; index++)
                {
                    table.Cell().Padding(3f).Text($"R{index:D2}");
                    table.Cell().Padding(3f).Text($"Row {index} of a table whose header repeats on every page");
                }
            });
        });
    }));

    private static byte[] Photograph()
    {
        using SKBitmap bitmap = new SKBitmap(400, 300);
        using SKCanvas canvas = new SKCanvas(bitmap);
        using SKPaint paint = new SKPaint();

        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(400, 300),
            [new SKColor(30, 90, 160), new SKColor(220, 120, 60)],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, 400, 300, paint);

        paint.Shader = null;
        paint.Color = SKColors.White;
        canvas.DrawCircle(200, 150, 60, paint);

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
