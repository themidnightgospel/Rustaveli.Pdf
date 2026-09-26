using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class ContentStreamBuilderTests
{
    private static readonly PdfName F1 = new PdfName("F1");

    private static string Build(Action<ContentStreamBuilder> build)
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        build(content);
        return Latin1.Text(content.Content);
    }

    private static string InText(Action<ContentStreamBuilder> build) => Build(content =>
    {
        content.BeginText();
        build(content);
        content.EndText();
    });

    [Fact]
    public void WritesGraphicsStateOperators()
    {
        string written = Build(content =>
        {
            content.SaveState();
            content.Transform(1, 0, 0, -1, 0, 841.89);
            content.SetLineWidth(0.5);
            content.SetLineCap(PdfLineCap.Round);
            content.SetLineJoin(PdfLineJoin.Bevel);
            content.SetMiterLimit(4);
            content.SetGraphicsState(new PdfName("GS1"));
            content.RestoreState();
        });

        Assert.Equal("q\n1 0 0 -1 0 841.89 cm\n0.5 w\n1 J\n2 j\n4 M\n/GS1 gs\nQ\n", written);
    }

    [Theory]
    [InlineData(nameof(PdfLineCap.Butt), "0 J\n")]
    [InlineData(nameof(PdfLineCap.ProjectingSquare), "2 J\n")]
    public void WritesLineCapsAsTheirCodes(string cap, string expected)
    {
        Assert.Equal(expected, Build(content => content.SetLineCap((PdfLineCap)Enum.Parse(typeof(PdfLineCap), cap))));
    }

    [Theory]
    [InlineData(nameof(PdfLineJoin.Miter), "0 j\n")]
    [InlineData(nameof(PdfLineJoin.Round), "1 j\n")]
    public void WritesLineJoinsAsTheirCodes(string join, string expected)
    {
        Assert.Equal(expected, Build(content => content.SetLineJoin((PdfLineJoin)Enum.Parse(typeof(PdfLineJoin), join))));
    }

    [Fact]
    public void WritesPathConstructionAndPainting()
    {
        string written = Build(content =>
        {
            content.MoveTo(10, 20);
            content.LineTo(30.25, 40);
            content.CurveTo(1, 2, 3, 4, 5, 6);
            content.ClosePath();
            content.Rectangle(0, 0, 595.28, 841.89);
            content.Fill();
            content.FillEvenOdd();
            content.Stroke();
            content.FillAndStroke();
            content.Clip();
            content.ClipEvenOdd();
            content.EndPath();
        });

        Assert.Equal("10 20 m\n30.25 40 l\n1 2 3 4 5 6 c\nh\n0 0 595.28 841.89 re\nf\nf*\nS\nB\nW\nW*\nn\n", written);
    }

    [Fact]
    public void WritesDeviceColours()
    {
        string written = Build(content =>
        {
            content.SetFillGray(0.5);
            content.SetStrokeGray(1);
            content.SetFillRgb(1, 0.5, 0);
            content.SetStrokeRgb(0.1, 0.2, 0.3);
            content.SetFillCmyk(0, 0.1, 0.2, 0.3);
            content.SetStrokeCmyk(1, 1, 1, 1);
        });

        Assert.Equal("0.5 g\n1 G\n1 0.5 0 rg\n0.1 0.2 0.3 RG\n0 0.1 0.2 0.3 k\n1 1 1 1 K\n", written);
    }

    [Fact]
    public void WritesColoursInNamedColourSpaces()
    {
        PdfName spot = new PdfName("CS1");
        PdfName pattern = new PdfName("P1");

        string written = Build(content =>
        {
            content.SetFillColorSpace(spot);
            content.SetStrokeColorSpace(new PdfName("DeviceRGB"));
            content.SetFillColor(new[] { 0.25 });
            content.SetStrokeColor(new[] { 1.0, 0, 0 });
            content.SetFillColorN(new[] { 0.75 });
            content.SetStrokeColorN(new[] { 0.5 });
            content.SetFillColorN(ReadOnlySpan<double>.Empty, pattern);
            content.SetStrokeColorN(new[] { 0.1, 0.2 }, pattern);
        });

        Assert.Equal("/CS1 cs\n/DeviceRGB CS\n0.25 sc\n1 0 0 SC\n0.75 scn\n0.5 SCN\n/P1 scn\n0.1 0.2/P1 SCN\n", written);
    }

    [Fact]
    public void WritesDashPatterns()
    {
        Assert.Equal("[3 1.5]0.5 d\n", Build(content => content.SetDashPattern(new[] { 3, 1.5 }, 0.5)));
        Assert.Equal("[0 2]0 d\n", Build(content => content.SetDashPattern(new[] { 0.0, 2 }, 0)));
        Assert.Equal("[]0 d\n", Build(content => content.SetDashPattern(ReadOnlySpan<double>.Empty, 0)));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void RefusesNegativeDashLengths(double length)
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => content.SetDashPattern(new[] { 2, length }, 0));

        Assert.Equal("dashes", exception.ParamName);
        Assert.Equal(0, content.Length);
    }

    [Fact]
    public void RefusesADashPatternOfZeros()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => content.SetDashPattern(new[] { 0.0, 0 }, 0));

        Assert.Equal("dashes", exception.ParamName);
        Assert.Equal(0, content.Length);
    }

    [Fact]
    public void WritesXObjectPlacement()
    {
        Assert.Equal("q\n100 0 0 50 10 20 cm\n/X1 Do\nQ\n", Build(content =>
        {
            content.SaveState();
            content.Transform(100, 0, 0, 50, 10, 20);
            content.PaintXObject(new PdfName("X1"));
            content.RestoreState();
        }));
    }

    [Fact]
    public void WritesTextState()
    {
        string written = Build(content =>
        {
            content.SetFont(F1, 12);
            content.SetCharacterSpacing(0.25);
            content.SetWordSpacing(-1);
            content.SetHorizontalScaling(90);
            content.SetLeading(14.4);
            content.SetTextRise(3);
            content.SetTextRenderingMode(PdfTextRenderingMode.Invisible);
            content.SetTextRenderingMode(PdfTextRenderingMode.Clip);
        });

        Assert.Equal("/F1 12 Tf\n0.25 Tc\n-1 Tw\n90 Tz\n14.4 TL\n3 Ts\n3 Tr\n7 Tr\n", written);
    }

    [Fact]
    public void WritesTextObjects()
    {
        string written = InText(content =>
        {
            content.SetFont(F1, 10);
            content.MoveTextPosition(72, 700);
            content.ShowText("Hi (there)"u8);
            content.SetTextMatrix(1, 0, 0, 1, 72, 680);
            content.ShowText(new byte[] { 0x00, 0x12, 0x00, 0x34 });
        });

        Assert.Equal("BT\n/F1 10 Tf\n72 700 Td\n(Hi \\(there\\))Tj\n1 0 0 1 72 680 Tm\n<00120034>Tj\nET\n", written);
    }

    [Fact]
    public void WritesTextWithPositioningAdjustments()
    {
        string written = InText(content =>
        {
            content.BeginTextArray();
            content.AppendText("A"u8);
            content.AppendAdjustment(120);
            content.AppendText("V"u8);
            content.AppendAdjustment(-250.5);
            content.AppendAdjustment(10);
            content.AppendText(new byte[] { 0x00, 0x01 });
            content.EndTextArray();
        });

        Assert.Equal("BT\n[(A)120(V)-250.5 10<0001>]TJ\nET\n", written);
    }

    [Fact]
    public void TracksTheStateItIsIn()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        Assert.Equal(0, content.StateDepth);
        Assert.False(content.InTextObject);

        content.SaveState();
        content.SaveState();
        Assert.Equal(2, content.StateDepth);

        content.RestoreState();
        content.BeginText();
        Assert.Equal(1, content.StateDepth);
        Assert.True(content.InTextObject);

        content.EndText();
        Assert.False(content.InTextObject);
    }

    [Fact]
    public void RefusesToRestoreMoreThanWasSaved()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.SaveState();
        content.RestoreState();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => content.RestoreState());

        Assert.Equal("Q without a matching q.", exception.Message);
        Assert.Equal("q\nQ\n", Latin1.Text(content.Content));
    }

    [Fact]
    public void RefusesNestedTextObjects()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.BeginText();

        Assert.Throws<InvalidOperationException>(() => content.BeginText());
        Assert.Equal("BT\n", Latin1.Text(content.Content));
    }

    [Fact]
    public void RefusesGraphicsStateStackOperatorsInsideText()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.SaveState();
        content.BeginText();

        InvalidOperationException save = Assert.Throws<InvalidOperationException>(() => content.SaveState());
        Assert.Throws<InvalidOperationException>(() => content.RestoreState());

        Assert.Equal("q is not allowed inside a text object; call EndText first.", save.Message);
        Assert.Equal(1, content.StateDepth);
        Assert.Equal("q\nBT\n", Latin1.Text(content.Content));
    }

    [Fact]
    public void RefusesTextPositioningAndShowingOutsideText()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => content.ShowText("a"u8));
        Assert.Throws<InvalidOperationException>(() => content.MoveTextPosition(1, 2));
        Assert.Throws<InvalidOperationException>(() => content.SetTextMatrix(1, 0, 0, 1, 0, 0));
        Assert.Throws<InvalidOperationException>(() => content.BeginTextArray());
        Assert.Throws<InvalidOperationException>(() => content.EndText());

        Assert.Equal("Tj is only valid inside a text object; call BeginText first.", exception.Message);
        Assert.Equal(0, content.Length);
    }

    [Fact]
    public void RefusesOtherOperatorsWhileATextArrayIsOpen()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.BeginText();
        content.BeginTextArray();
        content.AppendText("a"u8);
        string before = Latin1.Text(content.Content);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => content.MoveTo(1, 2));
        Assert.Throws<InvalidOperationException>(() => content.Fill());
        Assert.Throws<InvalidOperationException>(() => content.SetLineWidth(1));
        Assert.Throws<InvalidOperationException>(() => content.SetLineCap(PdfLineCap.Butt));
        Assert.Throws<InvalidOperationException>(() => content.SetFont(F1, 1));
        Assert.Throws<InvalidOperationException>(() => content.SetDashPattern(new[] { 1.0 }, 0));
        Assert.Throws<InvalidOperationException>(() => content.SetFillColorN(ReadOnlySpan<double>.Empty, F1));
        Assert.Throws<InvalidOperationException>(() => content.BeginTextArray());
        Assert.Throws<InvalidOperationException>(() => content.EndText());

        Assert.Equal("A TJ array is open; call EndTextArray first.", exception.Message);
        Assert.Equal(before, Latin1.Text(content.Content));
    }

    [Fact]
    public void RefusesArrayContentWithoutAnOpenArray()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.BeginText();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => content.AppendText("a"u8));
        Assert.Throws<InvalidOperationException>(() => content.AppendAdjustment(1));
        Assert.Throws<InvalidOperationException>(() => content.EndTextArray());

        Assert.Equal("No TJ array is open; call BeginTextArray first.", exception.Message);
        Assert.Equal("BT\n", Latin1.Text(content.Content));
    }

    [Fact]
    public void RefusesNullNames()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();

        Assert.Throws<ArgumentNullException>(() => content.PaintXObject(null!));
        Assert.Throws<ArgumentNullException>(() => content.SetFont(null!, 1));
    }

    [Fact]
    public void ConfirmsABalancedStream()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.SaveState();
        content.BeginText();
        content.EndText();
        content.RestoreState();

        content.EnsureComplete();

        Assert.Equal(0, content.StateDepth);
    }

    [Fact]
    public void ReportsAStreamLeftInsideText()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.BeginText();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => content.EnsureComplete());

        Assert.Equal("A text object is still open; call EndText.", exception.Message);
    }

    [Fact]
    public void ReportsAStreamWithStatesLeftSaved()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.SaveState();
        content.SaveState();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => content.EnsureComplete());

        Assert.Equal("2 saved graphics state(s) were never restored.", exception.Message);
    }
}
