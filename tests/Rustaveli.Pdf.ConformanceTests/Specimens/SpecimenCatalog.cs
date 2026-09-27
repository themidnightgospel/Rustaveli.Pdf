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
        new Specimen("frames-and-rules", FramesAndRules),
        new Specimen("images", Images),
        new Specimen("image-sources", ImageSources),
        new Specimen("layers-and-decoration", LayersAndDecoration),
        new Specimen("long-flow", LongFlow),
        new Specimen("text-strokes", TextStrokes),
        new Specimen("paragraphs", Paragraphs),
        new Specimen("complex-scripts", ComplexScripts)
    ];

    public static TheoryData<Specimen> Cases()
    {
        TheoryData<Specimen> data = new TheoryData<Specimen>();
        foreach (Specimen specimen in All)
            data.Add(specimen);
        return data;
    }

    private static Document Page(Action<IFrame> content, Action<Section>? configure = null) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A4;
            section.Margins = Sides.All(40f);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(10f);
            configure?.Invoke(section);
            content(section.Body());
        }));

    /// <summary>
    /// Scripts shaped by HarfBuzz: Arabic joining with its marks placed, right to left among English, and Devanagari
    /// conjuncts and reordered vowel signs.
    /// </summary>
    private static Document ComplexScripts() => Page(content => content.Stack(stack =>
    {
        stack.SpaceBetween(14f);

        stack.Add().RightToLeft().Text(text =>
        {
            text.DefaultType(type => type.WithTypeface("Noto Sans Arabic").WithPointSize(22f));
            text.Run("السلام عليكم ورحمة الله. ");
            text.Run("بَبُبِ");
        });

        stack.Add().Text(text =>
        {
            text.DefaultType(type => type.WithTypeface(TestFonts.Sans, "Noto Sans Arabic").WithPointSize(16f));
            text.Run("English with Arabic, العربية, in the middle of a sentence.");
        });

        stack.Add().Text(text =>
        {
            text.DefaultType(type => type.WithTypeface("Noto Sans Devanagari").WithPointSize(22f));
            text.Run("नमस्ते दुनिया। किताब, हिन्दी, क्षत्रिय।");
        });

        stack.Add().Width(260f).Stroke(0.5f).StrokeInk(TestInks.Grey).Inset(6f).RightToLeft().Text(text =>
        {
            text.Justified();
            text.DefaultType(type => type.WithTypeface("Noto Sans Arabic").WithPointSize(14f));
            text.Run("هذا نص عربي طويل بما يكفي ليلتف على عدة أسطر ويضبط من الجانبين، ليظهر أن الكلمات تبقى متصلة.");
        });
    }));

    /// <summary>Paragraph settings: justification, alignment by direction, line limits, breaking anywhere, inline frames.</summary>
    private static Document Paragraphs() => Page(content => content.Stack(stack =>
    {
        const string Prose = "Typesetting is the composition of text by means of arranging physical type or its digital "
            + "equivalents. Stored letters and other symbols are retrieved and ordered according to a language's "
            + "orthography for visual display.";

        stack.SpaceBetween(14f);

        stack.Add().Width(260f).Stroke(0.5f).StrokeInk(TestInks.Grey).Inset(6f).Text(text =>
        {
            text.Justified();
            text.FirstLineIndent(12f);
            text.Run(Prose);
            text.Run("\nThe last line of each paragraph sits flush against the start.");
        });

        stack.Add().Width(260f).Stroke(0.5f).StrokeInk(TestInks.Grey).Inset(6f).Text(text =>
        {
            text.FlushEnd();
            text.Run("Flush against the end of the line.");
        });

        stack.Add().Width(260f).Stroke(0.5f).StrokeInk(TestInks.Grey).Inset(6f).RightToLeft().Text(text =>
        {
            text.Justified();
            text.FirstLineIndent(12f);
            text.Run(Prose);
        });

        stack.Add().Width(260f).Stroke(0.5f).StrokeInk(TestInks.Grey).Inset(6f).Text(text =>
        {
            text.MaxLines(2);
            text.Run(Prose);
        });

        stack.Add().Width(260f).Stroke(0.5f).StrokeInk(TestInks.Grey).Inset(6f).Text(text =>
        {
            text.Run("An identifier: ");
            text.Run("urn:uuid:6e8bc430-9c3a-11d9-9669-0800200c9a66/chapter/section/paragraph").BreakAnywhere();
        });

        stack.Add().Text(text =>
        {
            text.DefaultType(type => type.WithPointSize(14f));

            foreach (InlinePosition position in new[] { InlinePosition.OnBaseline, InlinePosition.BelowBaseline, InlinePosition.TextTop, InlinePosition.TextBottom, InlinePosition.Middle })
            {
                text.Run(" " + position + " ");
                text.Inline(frame => frame.Width(10f).Height(10f).Fill(TestInks.Teal), position);
            }
        });
    }));

    /// <summary>Every stroke style under, through and over type, in the font's own weight and in others.</summary>
    private static Document TextStrokes() => Page(content => content.Stack(stack =>
    {
        stack.SpaceBetween(12f);

        foreach (StrokeStyle style in new[] { StrokeStyle.Solid, StrokeStyle.Double, StrokeStyle.Dotted, StrokeStyle.Dashed, StrokeStyle.Wavy })
        {
            stack.Add().Text(text =>
            {
                text.DefaultType(type => type.WithPointSize(16f).WithStrokeStyle(style));
                text.Run(style + ": ");
                text.Run("underlined").Underline();
                text.Run(", ");
                text.Run("struck").StrikeThrough();
                text.Run(", ");
                text.Run("overlined").Overline();
                text.Run(" and ");
                text.Run("all three").Underline().StrikeThrough().Overline().StrokeInk(TestInks.Red);
            });
        }

        stack.Add().Text(text =>
        {
            text.DefaultType(type => type.WithPointSize(16f));
            text.Run("Heavy").Underline().StrokeWeight(2f).StrokeInk(TestInks.Teal);
            text.Run(", hairline").Underline().StrokeWeight(0.25f);
            text.Run(", misspelt").Underline().StrokeStyle(StrokeStyle.Wavy).StrokeInk(TestInks.Red);
            text.Run(" and ");
            text.Run("raised").Superscript().Underline().Overline();
        });
    }));

    private static Document Gallery() => Page(content => content.Stack(stack =>
    {
        stack.SpaceBetween(14f);

        stack.Add().Text(text =>
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

        stack.Add().Text(text =>
        {
            text.FirstLineIndent(18f);
            text.Run("A paragraph long enough to wrap across several lines, with a first-line indent, so that line "
                + "breaking, indentation and the measured width of every word all show up in the rendered page.");
        });

        stack.Add().Text(text =>
        {
            text.Centered();
            text.Run("Centred").Ink(TestInks.Teal);
        });

        stack.Add().Text(text =>
        {
            text.FlushRight();
            text.Run("Right aligned").Ink(TestInks.Teal);
        });

        stack.Add().Columns(columns =>
        {
            columns.Gutter(10f);
            columns.Share().Stroke(1f).StrokeInk(TestInks.Grey).Inset(8f).Text("Stroked frame");
            columns.Share().Height(60f).Fill(TestInks.Indigo).Inset(8f).Centered().Middle()
                .Text(text => text.Run("Centred on both axes").Ink(TestInks.White));
            columns.Fixed(60f).Height(60f).Placeholder(TestInks.GreyLighten3);
        });

        stack.Add().Columns(columns =>
        {
            columns.Gutter(10f);
            columns.Share().Fill(TestInks.AmberLighten4).RoundCorners(8f).Inset(8f).Text("Rounded fill");
            columns.Share().Stroke(1f).StrokeInk(TestInks.Teal).RoundCorners(8f).Inset(8f).Text("Rounded outline");
            columns.Natural().Fill(TestInks.GreyLighten4).Inset(8f).Text("Natural");
        });

        stack.Add().Columns(columns =>
        {
            columns.Gutter(14f);
            columns.Share().List(list =>
            {
                list.Add().Text("A bulleted item");
                list.Add().Text("Another, long enough to wrap so continuation lines clear the marker");
            });
            columns.Share().List(list =>
            {
                list.Numbered();
                list.Add().Text("Numbered");
                list.Add().Text("items");
            });
        });

        stack.Add().Table(table =>
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

        stack.Add().RightToLeft().Columns(columns =>
        {
            columns.Gutter(6f);
            columns.Fixed(60f).Fill(TestInks.Teal).Inset(4f).Text("first");
            columns.Fixed(60f).Fill(TestInks.Cyan).Inset(4f).Text("second");
            columns.Share().Text("right to left");
        });

        stack.Add().Anchor("destination").Text(text =>
        {
            text.Run("An anchor with a ");
            text.Link("link", "https://example.com").Ink(TestInks.Blue).Underline();
            text.Run(" and a ");
            text.CrossReference("cross-reference", "destination").Ink(TestInks.Blue).Underline();
            text.Run(".");
        });
    }));

    private static Document Transforms() => Page(content => content.Stack(stack =>
    {
        stack.SpaceBetween(16f);

        stack.Add().Columns(columns =>
        {
            columns.Gutter(12f);
            columns.Fixed(80f).Height(40f).TurnLeft().Fill(TestInks.TealLighten3).Text("left");
            columns.Fixed(80f).Height(40f).TurnRight().Fill(TestInks.TealLighten3).Text("right");
            columns.Fixed(80f).MirrorHorizontal().Fill(TestInks.AmberLighten3).Text("mirrored");
            columns.Fixed(80f).MirrorVertical().Fill(TestInks.AmberLighten3).Text("mirrored");
            columns.Fixed(80f).MirrorBoth().Fill(TestInks.AmberLighten3).Text("both");
        });

        stack.Add().Columns(columns =>
        {
            columns.Gutter(12f);
            columns.Fixed(120f).Scale(0.5f).Fill(TestInks.PinkLighten3).Inset(6f).Text("half size");
            columns.Fixed(120f).Scale(1.5f, 1f).Fill(TestInks.PinkLighten3).Inset(6f).Text("stretched");
            columns.Fixed(120f).ShiftAcross(10f).ShiftDown(6f).Fill(TestInks.PinkLighten3).Inset(6f).Text("moved");
        });

        stack.Add().Width(160f).Height(24f).ShrinkToFit().Text("Scaled down until this whole sentence fits the box it was given.");

        stack.Add().Width(120f).Proportion(2f).Fill(TestInks.LimeLighten3).Centered().Middle().Text("2 : 1");
    }));

    /// <summary>
    /// Frames rounded corner by corner and stroked inside, on or outside their edge; content turned by any angle;
    /// gradients filling frames, strokes and rules; shadows soft, sharp, spread and shrunk; and rules in every stroke
    /// style and in a pattern of dashes.
    /// </summary>
    private static Document FramesAndRules() => Page(content => content.Stack(stack =>
    {
        stack.SpaceBetween(16f);

        stack.Add().Columns(columns =>
        {
            columns.Gutter(16f);
            columns.Fixed(100f).Height(50f).Fill(TestInks.TealLighten3).RoundCorners(0, 12, 24, 6).Centered().Middle().Text("corners");
            columns.Fixed(100f).Height(50f).Stroke(4f).StrokeInk(TestInks.Grey).AlignStroke(StrokeAlignment.Inside).Fill(TestInks.AmberLighten3).Centered().Middle().Text("inside");
            columns.Fixed(100f).Height(50f).Stroke(4f).StrokeInk(TestInks.Grey).AlignStroke(StrokeAlignment.Center).Fill(TestInks.AmberLighten3).Centered().Middle().Text("centred");
            columns.Fixed(100f).Height(50f).Stroke(4f).StrokeInk(TestInks.Grey).AlignStroke(StrokeAlignment.Outside).RoundCorners(10).Fill(TestInks.AmberLighten3).RoundCorners(10).Centered().Middle().Text("outside");
        });

        stack.Add().Height(70f).Columns(columns =>
        {
            columns.Gutter(24f);
            columns.Fixed(80f).Rotate(15f).Fill(TestInks.PinkLighten3).Centered().Middle().Text("15°");
            columns.Fixed(80f).Rotate(-30f).Fill(TestInks.PinkLighten3).Centered().Middle().Text("-30°");
            columns.Fixed(80f).Rotate(90f).Fill(TestInks.PinkLighten3).Centered().Middle().Text("90°");
        });

        stack.Add().Columns(columns =>
        {
            columns.Gutter(16f);
            columns.Fixed(100f).Height(50f).Fill(Gradient.Across(TestInks.TealLighten3, TestInks.AmberLighten3, TestInks.PinkLighten3)).Centered().Middle().Text("across");
            columns.Fixed(100f).Height(50f).Fill(new Gradient(45f, TestInks.TealLighten3, TestInks.Grey)).RoundCorners(12).Centered().Middle().Text("45°");
            columns.Fixed(100f).Height(50f).Stroke(5f).StrokeInk(Gradient.Down(TestInks.PinkLighten3, TestInks.Grey)).Centered().Middle().Text("stroke");
            columns.Fixed(100f).Height(50f).Stroke(5f).StrokeInk(Gradient.Across(TestInks.PinkLighten3, TestInks.Grey)).RoundCorners(12).Centered().Middle().Text("rounded");
        });

        stack.Add().Columns(columns =>
        {
            columns.Gutter(24f);
            columns.Fixed(100f).Height(50f).DropShadow(TestInks.Grey, 8f, 4f, 4f).Fill(Ink.White).Centered().Middle().Text("soft");
            columns.Fixed(100f).Height(50f).DropShadow(TestInks.Grey, 0f, 6f, 6f).Fill(Ink.White).Centered().Middle().Text("sharp");
            columns.Fixed(100f).Height(50f).DropShadow(new Shadow(TestInks.PinkLighten3, 16f, Spread: 4f)).RoundCorners(12).Fill(Ink.White).RoundCorners(12).Centered().Middle().Text("glow");
            columns.Fixed(100f).Height(50f).DropShadow(new Shadow(Ink.Black.WithOpacity(0.4f), 6f, new Offset(0, 8), -4f)).Fill(TestInks.AmberLighten3).Centered().Middle().Text("lifted");
        });

        stack.Add().Rule(4f, Gradient.Across(TestInks.TealLighten3, TestInks.PinkLighten3));
        stack.Add().Rule(3f, Gradient.Across(TestInks.PinkLighten3, TestInks.Grey), [9, 3]);
        stack.Add().Rule(2f, TestInks.Grey, StrokeStyle.Dashed);
        stack.Add().Rule(2f, TestInks.Grey, StrokeStyle.Dotted);
        stack.Add().Rule(3f, TestInks.Grey, StrokeStyle.Double);
        stack.Add().Rule(1.5f, TestInks.Grey, StrokeStyle.Wavy);
        stack.Add().Rule(2f, TestInks.Grey, [8, 3, 2, 3]);
        stack.Add().Rule(2f, TestInks.Grey, [6]);

        stack.Add().Height(60f).Columns(columns =>
        {
            columns.Share().Text("dashed on the left");
            columns.Fixed(20f).Centered().VerticalRule(2f, TestInks.Grey, [4, 2]);
            columns.Share().Text("wavy on the right");
            columns.Fixed(20f).Centered().VerticalRule(1.5f, TestInks.Grey, StrokeStyle.Wavy);
        });
    }));

    /// <summary>
    /// One image per way an image reaches the page: a JPEG as it is, turned by its EXIF orientation, in CMYK, in
    /// grey, with an ICC profile; a PNG with a palette, an alpha channel, a colour key, sixteen bits and a profile.
    /// </summary>
    private static Document ImageSources() => Page(content => content.Table(table =>
    {
        string[] fixtures =
        [
            "jpeg-baseline.jpg", "jpeg-exif-orientation6.jpg", "jpeg-cmyk-adobe.jpg", "jpeg-gray.jpg", "jpeg-icc.jpg",
            "basn3p08.png", "basn6a08.png", "tbrn2c08.png", "basi0g16.png", "png-iccp.png",
        ];

        table.Columns(columns =>
        {
            for (int column = 0; column < 5; column++)
                columns.Share();
        });

        foreach (string fixture in fixtures)
        {
            table.Cell().Inset(4f).Stack(stack =>
            {
                stack.SpaceBetween(4f);
                stack.Add().Height(80f).Fill(TestInks.GreyLighten4).Image(
                    RasterImage.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "images", fixture)),
                    ImageFitting.Proportionally);
                stack.Add().Text(text => text.Run(fixture).PointSize(7f));
            });
        }
    }));

    private static Document Images() => Page(content => content.Stack(stack =>
    {
        stack.SpaceBetween(10f);

        byte[] photograph = Photograph();

        // Each fit mode gets a box it can satisfy: fitting the width needs free height, and fitting the height needs
        // free width. Area and unproportional fit any box.
        stack.Add().Text("Width");
        stack.Add().Width(200f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(RasterImage.FromBytes(photograph), ImageFitting.FitWidth);
        stack.Add().Text("Height");
        stack.Add().Height(100f).FlushLeft().Stroke(0.5f).StrokeInk(TestInks.Grey).Image(RasterImage.FromBytes(photograph), ImageFitting.FitHeight);
        stack.Add().Text("Area");
        stack.Add().Width(300f).Height(120f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(RasterImage.FromBytes(photograph), ImageFitting.Proportionally);
        stack.Add().Text("Unproportional");
        stack.Add().Width(300f).Height(120f).Stroke(0.5f).StrokeInk(TestInks.Grey).Image(RasterImage.FromBytes(photograph), ImageFitting.Stretch);
    }));

    private static Document LayersAndDecoration() => Page(content => content.Stack(stack =>
    {
        stack.SpaceBetween(16f);

        stack.Add().Height(160f).Layered(layers =>
        {
            layers.Layer().Centered().Middle().Text(text => text.Run("WATERMARK").PointSize(40f).Ink(TestInks.GreyLighten3));
            layers.BaseLayer().Inset(10f).Text("The base layer sets the size; other layers draw behind or in front of it.");
            layers.Layer().FlushRight().FlushBottom().Inset(6f).Text(text => text.Run("corner").Ink(TestInks.Red));
        });

        stack.Add().Banded(bands =>
        {
            bands.Head().Fill(TestInks.IndigoLighten4).Inset(6f).Text("Head");
            bands.Body().Inset(6f).Text("The body sits between the bands.");
            bands.Foot().Fill(TestInks.IndigoLighten4).Inset(6f).Text("Foot");
        });

        stack.Add().Rule(1f, TestInks.Grey);

        stack.Add().Height(60f).Columns(columns =>
        {
            columns.Share().Text("left of the rule");
            columns.Fixed(20f).Centered().VerticalRule(1f, TestInks.Grey);
            columns.Share().Text("right of the rule");
        });
    }));

    private static Document LongFlow() => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = PaperSizes.A5;
        section.Margins = Sides.All(30f);
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(9f);

        section.RunningHead().InsetBottom(6f).Text(text => text.Run("Running head").Bold());
        section.RunningFoot().Centered().Text(text =>
        {
            text.Run("Page ");
            text.Folio();
            text.Run(" of ");
            text.PageCount();
        });

        section.Body().Stack(stack =>
        {
            stack.SpaceBetween(6f);

            for (int index = 0; index < 8; index++)
            {
                stack.Add().Text($"Paragraph {index + 1}. The quick brown fox jumps over the lazy dog, and then keeps "
                    + "running across the page so that the paragraph wraps onto several lines and the flow reaches the "
                    + "bottom margin, forcing content onto the following page.");
            }

            stack.Add().Table(table =>
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
