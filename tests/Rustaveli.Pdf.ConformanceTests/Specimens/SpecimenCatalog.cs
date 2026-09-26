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

    private static Document Page(Action<IFrame> content, Action<Section>? configure = null) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = PaperSizes.A4;
            page.Margin = Sides.All(40f);
            page.DefaultTextStyle = TypeStyle.Default.FontFamilyOf(TestFonts.Sans).FontSizeOf(10f);
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
            text.Centered();
            text.Span("Centred").FontColor(TestInks.Teal);
        });

        column.Item().Text(text =>
        {
            text.FlushRight();
            text.Span("Right aligned").FontColor(TestInks.Teal);
        });

        column.Item().Row(row =>
        {
            row.Spacing(10f);
            row.RelativeItem().Stroke(1f).StrokeInk(TestInks.Grey).Inset(8f).Text("Bordered box");
            row.RelativeItem().Height(60f).Fill(TestInks.Indigo).Inset(8f).Centered().Middle()
                .Text(text => text.Span("Centred on both axes").FontColor(TestInks.White));
            row.ConstantItem(60f).Height(60f).Placeholder(TestInks.GreyLighten3);
        });

        column.Item().Row(row =>
        {
            row.Spacing(10f);
            row.RelativeItem().Fill(TestInks.AmberLighten4).RoundCorners(8f).Inset(8f).Text("Rounded fill");
            row.RelativeItem().Stroke(1f).StrokeInk(TestInks.Teal).RoundCorners(8f).Inset(8f).Text("Rounded outline");
            row.AutoItem().Fill(TestInks.GreyLighten4).Inset(8f).Text("Auto");
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
                header.Cell().Fill(TestInks.Indigo).Inset(4f).Text(text => text.Span("Code").Bold().FontColor(TestInks.White));
                header.Cell().Fill(TestInks.Indigo).Inset(4f).Text(text => text.Span("Description").Bold().FontColor(TestInks.White));
                header.Cell().Fill(TestInks.Indigo).Inset(4f).FlushRight().Text(text => text.Span("Amount").Bold().FontColor(TestInks.White));
            });
            for (int index = 1; index <= 6; index++)
            {
                Ink shade = index % 2 == 0 ? TestInks.White : TestInks.GreyLighten4;
                table.Cell().Fill(shade).Inset(4f).Text($"SKU-{index:D3}");
                table.Cell().Fill(shade).Inset(4f).Text($"Line item {index}");
                table.Cell().Fill(shade).Inset(4f).FlushRight().Text($"{index * 12.5m:F2}");
            }
            table.Cell().ColumnSpan(2).Inset(4f).FlushRight().Text(text => text.Span("Total").Bold());
            table.Cell().Inset(4f).FlushRight().Text(text => text.Span("262.50").Bold());
        });

        column.Item().RightToLeft().Row(row =>
        {
            row.Spacing(6f);
            row.ConstantItem(60f).Fill(TestInks.Teal).Inset(4f).Text("first");
            row.ConstantItem(60f).Fill(TestInks.Cyan).Inset(4f).Text("second");
            row.RelativeItem().Text("right to left");
        });

        column.Item().Anchor("destination").Text(text =>
        {
            text.Span("A named destination with an external ");
            text.Link("hyperlink", "https://example.com").FontColor(TestInks.Blue).Underline();
            text.Span(" and an internal ");
            text.CrossReference("jump", "destination").FontColor(TestInks.Blue).Underline();
            text.Span(".");
        });
    }));

    private static Document Transforms() => Page(content => content.Column(column =>
    {
        column.Spacing(16f);

        column.Item().Row(row =>
        {
            row.Spacing(12f);
            row.ConstantItem(80f).Height(40f).TurnLeft().Fill(TestInks.TealLighten3).Text("left");
            row.ConstantItem(80f).Height(40f).TurnRight().Fill(TestInks.TealLighten3).Text("right");
            row.ConstantItem(80f).MirrorHorizontal().Fill(TestInks.AmberLighten3).Text("flipped");
            row.ConstantItem(80f).MirrorVertical().Fill(TestInks.AmberLighten3).Text("flipped");
            row.ConstantItem(80f).MirrorBoth().Fill(TestInks.AmberLighten3).Text("over");
        });

        column.Item().Row(row =>
        {
            row.Spacing(12f);
            row.ConstantItem(120f).Scale(0.5f).Fill(TestInks.PinkLighten3).Inset(6f).Text("half size");
            row.ConstantItem(120f).Scale(1.5f, 1f).Fill(TestInks.PinkLighten3).Inset(6f).Text("stretched");
            row.ConstantItem(120f).ShiftAcross(10f).ShiftDown(6f).Fill(TestInks.PinkLighten3).Inset(6f).Text("moved");
        });

        column.Item().Width(160f).Height(24f).ShrinkToFit().Text("Scaled down until this whole sentence fits the box it was given.");

        column.Item().Width(120f).Proportion(2f).Fill(TestInks.LimeLighten3).Centered().Middle().Text("2 : 1");
    }));

    private static Document Images() => Page(content => content.Column(column =>
    {
        column.Spacing(10f);

        byte[] photograph = Photograph();

        // Each fit mode gets a box it can satisfy: fitting the width needs free height, and fitting the height needs
        // free width. Area and unproportional fit any box.
        column.Item().Text("Width");
        column.Item().Width(200f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.Width);
        column.Item().Text("Height");
        column.Item().Height(100f).FlushLeft().Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.Height);
        column.Item().Text("Area");
        column.Item().Width(300f).Height(120f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.Area);
        column.Item().Text("Unproportional");
        column.Item().Width(300f).Height(120f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.Unproportional);
    }));

    private static Document LayersAndDecoration() => Page(content => content.Column(column =>
    {
        column.Spacing(16f);

        column.Item().Height(160f).Layers(layers =>
        {
            layers.Layer().Centered().Middle().Text(text => text.Span("WATERMARK").FontSize(40f).FontColor(TestInks.GreyLighten3));
            layers.PrimaryLayer().Inset(10f).Text("The primary layer sets the size; other layers draw behind or in front of it.");
            layers.Layer().FlushRight().FlushBottom().Inset(6f).Text(text => text.Span("corner").FontColor(TestInks.Red));
        });

        column.Item().Decoration(decoration =>
        {
            decoration.Before().Fill(TestInks.IndigoLighten4).Inset(6f).Text("Before");
            decoration.Content().Inset(6f).Text("Decorated content sits between the bands.");
            decoration.After().Fill(TestInks.IndigoLighten4).Inset(6f).Text("After");
        });

        column.Item().Rule(1f, TestInks.Grey);

        column.Item().Height(60f).Row(row =>
        {
            row.RelativeItem().Text("left of the rule");
            row.ConstantItem(20f).Centered().VerticalRule(1f, TestInks.Grey);
            row.RelativeItem().Text("right of the rule");
        });
    }));

    private static Document LongFlow() => Document.Create(container => container.Page(page =>
    {
        page.Size = PaperSizes.A5;
        page.Margin = Sides.All(30f);
        page.DefaultTextStyle = TypeStyle.Default.FontFamilyOf(TestFonts.Sans).FontSizeOf(9f);

        page.Header().InsetBottom(6f).Text(text => text.Span("Running head").Bold());
        page.Footer().Centered().Text(text =>
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
                    header.Cell().Fill(TestInks.GreyLighten3).Inset(3f).Text("Code");
                    header.Cell().Fill(TestInks.GreyLighten3).Inset(3f).Text("Description");
                });
                for (int index = 1; index <= 40; index++)
                {
                    table.Cell().Inset(3f).Text($"R{index:D2}");
                    table.Cell().Inset(3f).Text($"Row {index} of a table whose header repeats on every page");
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
