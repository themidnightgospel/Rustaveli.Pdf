using Rustaveli.Pdf.Svg;

namespace Rustaveli.Pdf.UnitTests.Svg;

/// <summary>
/// The small languages inside SVG attributes: numbers, path data, transforms, lengths, colours and style sheets.
/// </summary>
public class SvgSyntaxTests
{
    // ---- Numbers ---------------------------------------------------------------------------------------------

    [Fact]
    public void NumbersNeedNoSeparatorWhereASignOrPointStartsTheNext() =>
        Assert.Equal([1f, -2.5f, 0.5f, 300f, 0.1f, 4f], new SvgNumbers("1-2.5.5 3e2,1e-1 +4").Rest());

    [Fact]
    public void AnExponentMustHaveDigits()
    {
        SvgNumbers numbers = new SvgNumbers("5em");

        Assert.Equal(5f, numbers.Next());
        Assert.Equal('e', numbers.Peek());
    }

    [Fact]
    public void ReadingStopsAtTheFirstThingThatIsNotANumber()
    {
        Assert.Equal([1f, 2f], new SvgNumbers("1 2 x 3").Rest());
        Assert.Throws<FormatException>(() => new SvgNumbers("x").Next());
        Assert.Throws<FormatException>(() => new SvgNumbers("-").Next());
    }

    [Fact]
    public void FlagsMayRunTogether()
    {
        SvgNumbers numbers = new SvgNumbers("01 1");

        Assert.False(numbers.NextFlag());
        Assert.True(numbers.NextFlag());
        Assert.True(numbers.NextFlag());
        Assert.Throws<FormatException>(() => numbers.NextFlag());
        Assert.Throws<FormatException>(() => new SvgNumbers("2").NextFlag());
    }

    // ---- Path data -------------------------------------------------------------------------------------------

    private static string Describe(VectorPath path)
    {
        List<string> parts = [];
        int point = 0;

        foreach (PathVerb verb in path.Verbs)
        {
            int count = verb switch { PathVerb.Cubic => 3, PathVerb.Close => 0, _ => 1 };
            parts.Add(verb.ToString()[0] + string.Concat(Enumerable.Range(point, count).Select(index => $" {path.Points[index].X:0.##},{path.Points[index].Y:0.##}")));
            point += count;
        }

        return string.Join(" | ", parts);
    }

    [Fact]
    public void MovesLinesAndClosesAbsoluteAndRelative()
    {
        Assert.Equal("M 10,10 | L 20,10 | L 20,30 | L 5,30 | C | M 12,15 | L 14,15",
            Describe(SvgPathData.Read("M10 10 L20 10 v20 H5 z m2 5 h2")));
    }

    [Fact]
    public void PairsAfterAMoveAreLines()
    {
        Assert.Equal("M 1,1 | L 2,2 | L 3,3", Describe(SvgPathData.Read("M1 1 2 2 3 3")));
        Assert.Equal("M 1,1 | L 3,3 | L 6,6", Describe(SvgPathData.Read("m1 1 2 2 3 3")));
    }

    [Fact]
    public void ACommandRepeatsForMoreNumbers() =>
        Assert.Equal("M 0,0 | L 1,0 | L 1,1", Describe(SvgPathData.Read("M0 0 l1 0 0 1")));

    [Fact]
    public void CubicsAndTheirSmoothContinuations()
    {
        Assert.Equal("M 0,0 | C 1,1 2,2 3,3 | C 4,4 5,5 6,6", Describe(SvgPathData.Read("M0 0 C1 1 2 2 3 3 S5 5 6 6")));
        Assert.Equal("M 0,0 | C 1,1 2,2 3,3 | C 4,4 5,5 6,6", Describe(SvgPathData.Read("M0 0 c1 1 2 2 3 3 s2 2 3 3")));

        // Without a curve before, the first control point is the pen.
        Assert.Equal("M 0,0 | L 1,0 | C 1,0 5,5 6,6", Describe(SvgPathData.Read("M0 0 L1 0 S5 5 6 6")));
    }

    [Fact]
    public void QuadraticsAndTheirSmoothContinuations()
    {
        string path = Describe(SvgPathData.Read("M0 0 Q 3 3 6 0 T 12 0"));
        string relative = Describe(SvgPathData.Read("M0 0 q 3 3 6 0 t 6 0"));

        Assert.Equal(path, relative);
        Assert.Equal("M 0,0 | C 2,2 4,2 6,0 | C 8,-2 10,-2 12,0", path);
        Assert.Equal("M 0,0 | C 0,0 2,0 6,0", Describe(SvgPathData.Read("M0 0 T 6 0")));
    }

    [Fact]
    public void ArcsWithFlagsRunTogether()
    {
        VectorPath path = SvgPathData.Read("M0 0 a5 5 0 1010 0");

        Assert.Equal(new Offset(10, 0), path.Points[path.Points.Count - 1]);
        Assert.Contains(PathVerb.Cubic, path.Verbs);
        Assert.Equal(new Offset(10, 0), SvgPathData.Read("M0 0 A5 5 0 0 1 10 0").Points.Last());
    }

    [Fact]
    public void ReadingStopsAtAnErrorAndKeepsWhatCameBefore()
    {
        Assert.Equal("M 0,0 | L 5,5", Describe(SvgPathData.Read("M0 0 L5 5 L x")));
        Assert.Equal("M 0,0", Describe(SvgPathData.Read("M0 0 X 5 5")));
        Assert.Equal(string.Empty, Describe(SvgPathData.Read("5 5")));
        Assert.Equal(string.Empty, Describe(SvgPathData.Read("  ")));
    }

    // ---- Transforms ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("matrix(1 2 3 4 5 6)", 1f, 2f, 3f, 4f, 5f, 6f)]
    [InlineData("translate(5)", 1f, 0f, 0f, 1f, 5f, 0f)]
    [InlineData("translate(5, 6)", 1f, 0f, 0f, 1f, 5f, 6f)]
    [InlineData("scale(2)", 2f, 0f, 0f, 2f, 0f, 0f)]
    [InlineData("scale(2 3)", 2f, 0f, 0f, 3f, 0f, 0f)]
    [InlineData("rotate(90)", 0f, 1f, -1f, 0f, 0f, 0f)]
    [InlineData("rotate(90 10 10)", 0f, 1f, -1f, 0f, 20f, 0f)]
    [InlineData("skewX(45)", 1f, 0f, 1f, 1f, 0f, 0f)]
    [InlineData("skewY(45)", 1f, 1f, 0f, 1f, 0f, 0f)]
    [InlineData("translate(10 0) scale(2)", 2f, 0f, 0f, 2f, 10f, 0f)]
    [InlineData("scale(2), translate(10 0)", 2f, 0f, 0f, 2f, 20f, 0f)]
    public void TransformsMakeOneMatrix(string list, float a, float b, float c, float d, float e, float f)
    {
        (float A, float B, float C, float D, float E, float F) matrix = SvgTransform.Read(list)!.Value;

        Assert.Equal(a, matrix.A, 3);
        Assert.Equal(b, matrix.B, 3);
        Assert.Equal(c, matrix.C, 3);
        Assert.Equal(d, matrix.D, 3);
        Assert.Equal(e, matrix.E, 3);
        Assert.Equal(f, matrix.F, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("wobble(3)")]
    [InlineData("matrix(1 2 3)")]
    [InlineData("translate(")]
    [InlineData("scale()")]
    [InlineData("rotate()")]
    [InlineData("skewX()")]
    [InlineData("skewY()")]
    [InlineData("translate()")]
    [InlineData("nothing")]
    public void NothingReadableIsNoTransform(string? list) =>
        Assert.Null(SvgTransform.Read(list));

    // ---- Lengths ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("12", 12f)]
    [InlineData("12px", 12f)]
    [InlineData("12pt", 16f)]
    [InlineData("1pc", 16f)]
    [InlineData("25.4mm", 96f)]
    [InlineData("2.54cm", 96f)]
    [InlineData("1in", 96f)]
    [InlineData("2em", 32f)]
    [InlineData("2ex", 16f)]
    [InlineData("50%", 100f)]
    [InlineData("1e1", 10f)]
    [InlineData("3furlongs", 3f)]
    [InlineData(" 7 ", 7f)]
    public void LengthsAreReadInPixels(string text, float pixels) =>
        Assert.Equal(pixels, SvgLength.Read(text, 200, -1), 3);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    public void NoLengthIsTheFallback(string? text) =>
        Assert.Equal(-1f, SvgLength.Read(text, 200, -1));

    [Theory]
    [InlineData("0.25", 0.25f)]
    [InlineData("25%", 0.25f)]
    [InlineData(null, 9f)]
    [InlineData("x", 9f)]
    public void FractionsArePlainOrPercentages(string? text, float fraction) =>
        Assert.Equal(fraction, SvgLength.Fraction(text, 9));

    // ---- Colours ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("red", 255, 0, 0, 1f)]
    [InlineData("RebeccaPurple", 0x66, 0x33, 0x99, 1f)]
    [InlineData("#0f8", 0, 255, 0x88, 1f)]
    [InlineData("#0f88", 0, 255, 0x88, 0.533f)]
    [InlineData("#102030", 0x10, 0x20, 0x30, 1f)]
    [InlineData("#10203080", 0x10, 0x20, 0x30, 0.502f)]
    [InlineData("rgb(10, 20, 30)", 10, 20, 30, 1f)]
    [InlineData("rgb(100%, 50%, 0%)", 255, 128, 0, 1f)]
    [InlineData("rgba(10 20 30 / 0.5)", 10, 20, 30, 0.5f)]
    [InlineData("rgb(300, -5, 20)", 255, 0, 20, 1f)]
    [InlineData("hsl(0, 100%, 50%)", 255, 0, 0, 1f)]
    [InlineData("hsl(60deg, 100%, 50%)", 255, 255, 0, 1f)]
    [InlineData("hsl(120, 100%, 50%)", 0, 255, 0, 1f)]
    [InlineData("hsl(180, 100%, 50%)", 0, 255, 255, 1f)]
    [InlineData("hsl(240, 100%, 50%)", 0, 0, 255, 1f)]
    [InlineData("hsla(300, 100%, 50%, 50%)", 255, 0, 255, 0.5f)]
    [InlineData("hsl(-60, 100%, 50%)", 255, 0, 255, 1f)]
    public void ColoursAreReadInEveryForm(string text, int red, int green, int blue, float opacity)
    {
        Ink ink = SvgColour.Read(text)!.Value;
        (float r, float g, float b) = ink.ToRgb();

        Assert.Equal((red, green, blue), ((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255)));
        Assert.Equal(opacity, ink.Opacity, 2);
    }

    [Fact]
    public void TransparentIsNoInkAtAll() =>
        Assert.True(SvgColour.Read("transparent")!.Value.IsTransparent);

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("chartreusish")]
    [InlineData("#12")]
    [InlineData("#zzzzzz")]
    [InlineData("rgb(1, 2)")]
    [InlineData("cmyk(1, 2, 3, 4)")]
    [InlineData("rgb(1, 2, 3")]
    public void WhatIsNoColourIsNull(string? text) =>
        Assert.Null(SvgColour.Read(text));

    [Fact]
    public void AnUnreadableChannelIsNothing() =>
        Assert.Equal(Ink.Rgb(0, 20, 30), SvgColour.Read("rgb(x, 20, 30)"));

    // ---- Style sheets ----------------------------------------------------------------------------------------

    private static Dictionary<string, string> Styled(string css, string element)
    {
        SvgStyleSheet sheet = new SvgStyleSheet();
        sheet.Add(css);
        Dictionary<string, string> styled = [];

        foreach (KeyValuePair<string, string> declaration in sheet.For(System.Xml.Linq.XElement.Parse(element)))
            styled[declaration.Key] = declaration.Value;

        return styled;
    }

    [Fact]
    public void RulesApplyByNameClassAndId()
    {
        const string Css = "rect { fill: red } .a { stroke: blue } #b { opacity: 0.5 } * { color: green }";

        Assert.Equal(
            new Dictionary<string, string> { ["fill"] = "red", ["stroke"] = "blue", ["opacity"] = "0.5", ["color"] = "green" },
            Styled(Css, "<rect class='a other' id='b'/>"));
        Assert.Equal(new Dictionary<string, string> { ["color"] = "green" }, Styled(Css, "<circle/>"));
    }

    [Fact]
    public void AMoreSpecificRuleWinsWhateverItsOrder()
    {
        Dictionary<string, string> styled = Styled("#b { fill: red } rect.a { fill: blue } .a { fill: green } rect { fill: grey }", "<rect class='a' id='b'/>");

        Assert.Equal("red", styled["fill"]);
    }

    [Fact]
    public void LaterRulesOfTheSameWeightWin() =>
        Assert.Equal("blue", Styled(".a { fill: red } .a { fill: blue }", "<rect class='a'/>")["fill"]);

    [Fact]
    public void ListsCommentsAndImportanceAreRead()
    {
        Dictionary<string, string> styled = Styled("/* note */ circle, .x { fill: red !important; ; bad; }", "<circle/>");

        Assert.Equal(new Dictionary<string, string> { ["fill"] = "red" }, styled);
    }

    [Fact]
    public void RulesWithSelectorsBeyondTheseAreLeftOut() =>
        Assert.Empty(Styled("g rect { fill: red } rect:hover { fill: blue } rect > x { fill: green } [a] { fill: grey }", "<rect/>"));

    [Fact]
    public void AnUnfinishedRuleIsLeftOut()
    {
        Assert.Empty(Styled("rect { fill: red", "<rect/>"));
        Assert.Empty(Styled("rect", "<rect/>"));
        Assert.Empty(Styled("/* never closed", "<rect/>"));
    }

    [Fact]
    public void AClassRuleNeedsEveryClass() =>
        Assert.Empty(Styled(".a.b { fill: red }", "<rect class='a'/>"));
}
