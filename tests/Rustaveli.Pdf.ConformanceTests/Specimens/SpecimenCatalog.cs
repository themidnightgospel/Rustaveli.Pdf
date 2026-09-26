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
        Document.Compose(container => container.Section(page =>
        {
            page.Trim = PaperSizes.A4;
            page.Margins = Sides.All(40f);
            page.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(10f);
            configure?.Invoke(page);
            content(page.Body());
        }));

    private static Document Gallery() => Page(content => content.Stack(column =>
    {
        column.SpaceBetween(14f);

        column.Add().Text(text =>
        {
            text.Run("Styles compose: ");
            text.Run("bold").Bold();
            text.Run(", ");
            text.Run("italic").Italic();
            text.Run(", ");
            text.Run("underlined").Underline();
            text.Run(", ");
            text.Run("struck through").StrikeThrough();
            text.Run(", ");
            text.Run("highlighted").Highlight(TestInks.Yellow);
            text.Run(" and coloured").Ink(TestInks.Red);
            text.Run(". H");
            text.Run("2").Subscript();
            text.Run("O and e");
            text.Run("iπ").Superscript();
            text.Run(".");
        });

        column.Add().Text(text =>
        {
            text.FirstLineIndent(18f);
            text.Run("A paragraph long enough to wrap across several lines, with a first-line indent, so that line "
                + "breaking, indentation and the measured width of every word all show up in the rendered page.");
        });

        column.Add().Text(text =>
        {
            text.Centered();
            text.Run("Centred").Ink(TestInks.Teal);
        });

        column.Add().Text(text =>
        {
            text.FlushRight();
            text.Run("Right aligned").Ink(TestInks.Teal);
        });

        column.Add().Columns(row =>
        {
            row.Gutter(10f);
            row.Share().Stroke(1f).StrokeInk(TestInks.Grey).Inset(8f).Text("Bordered box");
            row.Share().Height(60f).Fill(TestInks.Indigo).Inset(8f).Centered().Middle()
                .Text(text => text.Run("Centred on both axes").Ink(TestInks.White));
            row.Fixed(60f).Height(60f).Placeholder(TestInks.GreyLighten3);
        });

        column.Add().Columns(row =>
        {
            row.Gutter(10f);
            row.Share().Fill(TestInks.AmberLighten4).RoundCorners(8f).Inset(8f).Text("Rounded fill");
            row.Share().Stroke(1f).StrokeInk(TestInks.Teal).RoundCorners(8f).Inset(8f).Text("Rounded outline");
            row.Natural().Fill(TestInks.GreyLighten4).Inset(8f).Text("Auto");
        });

        column.Add().Columns(row =>
        {
            row.Gutter(14f);
            row.Share().List(list =>
            {
                list.Add().Text("A bulleted item");
                list.Add().Text("Another, long enough to wrap so continuation lines clear the marker");
            });
            row.Share().List(list =>
            {
                list.Numbered();
                list.Add().Text("Numbered");
                list.Add().Text("items");
            });
        });

        column.Add().Table(table =>
        {
            table.Columns(columns =>
            {
                columns.Fixed(70f);
                columns.Share();
                columns.Fixed(70f);
            });
            table.HeaderRows(header =>
            {
                header.Cell().Fill(TestInks.Indigo).Inset(4f).Text(text => text.Run("Code").Bold().Ink(TestInks.White));
                header.Cell().Fill(TestInks.Indigo).Inset(4f).Text(text => text.Run("Description").Bold().Ink(TestInks.White));
                header.Cell().Fill(TestInks.Indigo).Inset(4f).FlushRight().Text(text => text.Run("Amount").Bold().Ink(TestInks.White));
            });
            for (int index = 1; index <= 6; index++)
            {
                Ink shade = index % 2 == 0 ? TestInks.White : TestInks.GreyLighten4;
                table.Cell().Fill(shade).Inset(4f).Text($"SKU-{index:D3}");
                table.Cell().Fill(shade).Inset(4f).Text($"Line item {index}");
                table.Cell().Fill(shade).Inset(4f).FlushRight().Text($"{index * 12.5m:F2}");
            }
            table.Cell().SpanColumns(2).Inset(4f).FlushRight().Text(text => text.Run("Total").Bold());
            table.Cell().Inset(4f).FlushRight().Text(text => text.Run("262.50").Bold());
        });

        column.Add().RightToLeft().Columns(row =>
        {
            row.Gutter(6f);
            row.Fixed(60f).Fill(TestInks.Teal).Inset(4f).Text("first");
            row.Fixed(60f).Fill(TestInks.Cyan).Inset(4f).Text("second");
            row.Share().Text("right to left");
        });

        column.Add().Anchor("destination").Text(text =>
        {
            text.Run("A named destination with an external ");
            text.Link("hyperlink", "https://example.com").Ink(TestInks.Blue).Underline();
            text.Run(" and an internal ");
            text.CrossReference("jump", "destination").Ink(TestInks.Blue).Underline();
            text.Run(".");
        });
    }));

    private static Document Transforms() => Page(content => content.Stack(column =>
    {
        column.SpaceBetween(16f);

        column.Add().Columns(row =>
        {
            row.Gutter(12f);
            row.Fixed(80f).Height(40f).TurnLeft().Fill(TestInks.TealLighten3).Text("left");
            row.Fixed(80f).Height(40f).TurnRight().Fill(TestInks.TealLighten3).Text("right");
            row.Fixed(80f).MirrorHorizontal().Fill(TestInks.AmberLighten3).Text("flipped");
            row.Fixed(80f).MirrorVertical().Fill(TestInks.AmberLighten3).Text("flipped");
            row.Fixed(80f).MirrorBoth().Fill(TestInks.AmberLighten3).Text("over");
        });

        column.Add().Columns(row =>
        {
            row.Gutter(12f);
            row.Fixed(120f).Scale(0.5f).Fill(TestInks.PinkLighten3).Inset(6f).Text("half size");
            row.Fixed(120f).Scale(1.5f, 1f).Fill(TestInks.PinkLighten3).Inset(6f).Text("stretched");
            row.Fixed(120f).ShiftAcross(10f).ShiftDown(6f).Fill(TestInks.PinkLighten3).Inset(6f).Text("moved");
        });

        column.Add().Width(160f).Height(24f).ShrinkToFit().Text("Scaled down until this whole sentence fits the box it was given.");

        column.Add().Width(120f).Proportion(2f).Fill(TestInks.LimeLighten3).Centered().Middle().Text("2 : 1");
    }));

    private static Document Images() => Page(content => content.Stack(column =>
    {
        column.SpaceBetween(10f);

        byte[] photograph = Photograph();

        // Each fit mode gets a box it can satisfy: fitting the width needs free height, and fitting the height needs
        // free width. Area and unproportional fit any box.
        column.Add().Text("Width");
        column.Add().Width(200f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.FitWidth);
        column.Add().Text("Height");
        column.Add().Height(100f).FlushLeft().Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.FitHeight);
        column.Add().Text("Area");
        column.Add().Width(300f).Height(120f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.Proportionally);
        column.Add().Text("Unproportional");
        column.Add().Width(300f).Height(120f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(SkiaImage.FromBytes(photograph), ImageFitting.Stretch);
    }));

    private static Document LayersAndDecoration() => Page(content => content.Stack(column =>
    {
        column.SpaceBetween(16f);

        column.Add().Height(160f).Layered(layers =>
        {
            layers.Layer().Centered().Middle().Text(text => text.Run("WATERMARK").PointSize(40f).Ink(TestInks.GreyLighten3));
            layers.BaseLayer().Inset(10f).Text("The primary layer sets the size; other layers draw behind or in front of it.");
            layers.Layer().FlushRight().FlushBottom().Inset(6f).Text(text => text.Run("corner").Ink(TestInks.Red));
        });

        column.Add().Banded(decoration =>
        {
            decoration.Head().Fill(TestInks.IndigoLighten4).Inset(6f).Text("Before");
            decoration.Body().Inset(6f).Text("Decorated content sits between the bands.");
            decoration.Foot().Fill(TestInks.IndigoLighten4).Inset(6f).Text("After");
        });

        column.Add().Rule(1f, TestInks.Grey);

        column.Add().Height(60f).Columns(row =>
        {
            row.Share().Text("left of the rule");
            row.Fixed(20f).Centered().VerticalRule(1f, TestInks.Grey);
            row.Share().Text("right of the rule");
        });
    }));

    private static Document LongFlow() => Document.Compose(container => container.Section(page =>
    {
        page.Trim = PaperSizes.A5;
        page.Margins = Sides.All(30f);
        page.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(9f);

        page.RunningHead().InsetBottom(6f).Text(text => text.Run("Running head").Bold());
        page.RunningFoot().Centered().Text(text =>
        {
            text.Run("Page ");
            text.Folio();
            text.Run(" of ");
            text.PageCount();
        });

        page.Body().Stack(column =>
        {
            column.SpaceBetween(6f);

            for (int index = 0; index < 8; index++)
            {
                column.Add().Text($"Paragraph {index + 1}. The quick brown fox jumps over the lazy dog, and then keeps "
                    + "running across the page so that the paragraph wraps onto several lines and the flow reaches the "
                    + "bottom margin, forcing content onto the following page.");
            }

            column.Add().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.Fixed(60f);
                    columns.Share();
                });
                table.HeaderRows(header =>
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
