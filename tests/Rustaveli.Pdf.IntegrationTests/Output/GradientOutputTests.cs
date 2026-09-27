using System.Text;
using System.Text.RegularExpressions;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// How gradients come out in the file: as axial shading patterns placed in the page's space, painted by fills and
/// strokes through the pattern colour space, with the ink set again after them.
/// </summary>
public class GradientOutputTests
{
    private static readonly Ink Red = Ink.Rgb(255, 0, 0);
    private static readonly Ink Blue = Ink.Rgb(0, 0, 255);

    private static string Export(Action<StackComposer> compose) =>
        Encoding.Latin1.GetString(Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 400);
            section.Margins = Sides.All(0);
            section.Body().Stack(compose);
        })).ExportPdf(new PdfExportOptions { Compress = false }));

    [Fact]
    public void AFilledGradientIsAnAxialShadingAcrossTheFrame()
    {
        string pdf = Export(stack => stack.Add().Height(40).Fill(Gradient.Across(Red, Blue)));

        // Left to right across the 300-point frame, in the drawing's Y-down space, which the flip places on the page.
        Assert.Matches(@"/PatternType 2", pdf);
        Assert.Matches(@"/ShadingType 2\s*/ColorSpace\s*/DeviceRGB\s*/Coords\s*\[0 20 300 20\]", pdf);
        Assert.Matches(@"/FunctionType 2\s*/Domain\s*\[0 1\]\s*/C0\s*\[1 0 0\]\s*/C1\s*\[0 0 1\]\s*/N 1", pdf);
        Assert.Matches(@"/Extend\s*\[true true\]", pdf);
        Assert.Matches(@"/Matrix\s*\[1 0 0 -1 0 400\]", pdf);
        Assert.Matches(@"/Pattern cs\s*/P1 scn", pdf);
        Assert.Matches(@"/Pattern\s*<<\s*/P1 \d+ 0 R", pdf);
    }

    [Fact]
    public void MoreThanTwoInksAreStitchedAtEvenIntervals()
    {
        string pdf = Export(stack => stack.Add().Height(40).Fill(Gradient.Down(Red, Ink.Rgb(0, 255, 0), Blue)));

        Assert.Matches(@"/Coords\s*\[150 0 150 40\]", pdf);
        Assert.Matches(@"/FunctionType 3\s*/Domain\s*\[0 1\]\s*/Functions\s*\[<<", pdf);
        Assert.Matches(@"/Bounds\s*\[0\.5\]\s*/Encode\s*\[0 1 0 1\]", pdf);
    }

    [Fact]
    public void StopsInsideTheBlendHoldTheirInkToTheEnds()
    {
        string pdf = Export(stack => stack.Add().Height(40).Fill(new Gradient(0, new GradientStop(0.25f, Red), new GradientStop(0.75f, Blue))));

        // Red up to a quarter, the blend to three quarters, and blue from there.
        Assert.Matches(@"/FunctionType 3\s*/Domain\s*\[0 1\]", pdf);
        Assert.Matches(@"/Bounds\s*\[0\.25 0\.75\]\s*/Encode\s*\[0 1 0 1 0 1\]", pdf);
        Assert.Matches(@"/C0\s*\[1 0 0\]\s*/C1\s*\[1 0 0\]", pdf);
    }

    [Fact]
    public void ProcessInksBlendInCmyk()
    {
        string pdf = Export(stack => stack.Add().Height(40).Fill(Gradient.Across(Ink.Cmyk(1, 0, 0, 0), Ink.Cmyk(0, 0, 0, 1))));

        Assert.Matches(@"/ColorSpace\s*/DeviceCMYK", pdf);
        Assert.Matches(@"/C0\s*\[1 0 0 0\]\s*/C1\s*\[0 0 0 1\]", pdf);
    }

    [Fact]
    public void MixedInksBlendInRgb()
    {
        string pdf = Export(stack => stack.Add().Height(40).Fill(Gradient.Across(Ink.Cmyk(0, 0, 0, 1), Ink.Spot("Signal", Red))));

        Assert.Matches(@"/ColorSpace\s*/DeviceRGB", pdf);
        Assert.Matches(@"/C0\s*\[0 0 0\]\s*/C1\s*\[1 0 0\]", pdf);
    }

    [Fact]
    public void AStrokeIsPaintedThroughThePatternColourSpace()
    {
        string pdf = Export(stack => stack.Add().Height(40).Stroke(2).StrokeInk(Gradient.Across(Red, Blue)).Blank());

        Assert.Matches(@"/Pattern cs\s*/P1 scn", pdf);
        Assert.Matches(@"/Coords\s*\[0 20 300 20\]", pdf);
    }

    [Fact]
    public void ADashedRuleIsStrokedWithThePattern()
    {
        string pdf = Export(stack => stack.Add().Rule(2, Gradient.Across(Red, Blue), [4, 2]));

        Assert.Matches(@"/Pattern CS\s*/P1 SCN", pdf);
        Assert.Matches(@"/Coords\s*\[0 1 300 1\]", pdf);
    }

    [Fact]
    public void TheInkAfterAGradientIsWrittenAgain()
    {
        string pdf = Export(stack =>
        {
            stack.Add().Height(20).Fill(Red);
            stack.Add().Height(20).Fill(Gradient.Across(Red, Blue));
            stack.Add().Height(20).Fill(Red);
        });

        Assert.Equal(2, Regex.Matches(pdf, @"1 0 0 rg").Count);
    }

    [Fact]
    public void ATranslucentGradientIsDrawnAtItsOpacity()
    {
        string pdf = Export(stack => stack.Add().Height(40).Fill(Gradient.Across(Red.WithOpacity(0.5f), Blue.WithOpacity(0.5f))));

        Assert.Matches(@"/ca 0\.5", pdf);
    }

    [Fact]
    public void EachGradientIsItsOwnPattern()
    {
        string pdf = Export(stack =>
        {
            stack.Add().Height(20).Fill(Gradient.Across(Red, Blue));
            stack.Add().Height(20).Fill(Gradient.Across(Blue, Red));
        });

        // The second is drawn where the stack has moved to: its line is in that frame's own space, and the pattern's
        // matrix carries the move onto the page.
        Assert.Matches(@"/P2 scn", pdf);
        Assert.Equal(2, Regex.Matches(pdf, @"/Coords\s*\[0 10 300 10\]").Count);
        Assert.Matches(@"/Matrix\s*\[1 0 0 -1 0 380\]", pdf);
    }
}
