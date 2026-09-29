namespace Rustaveli.Pdf.UnitTests.Svg;

/// <summary>
/// SVG documents read into artwork and drawn at their own size, where a CSS pixel is three quarters of a point.
/// </summary>
public class SvgReaderTests
{
    private const string Namespaces = "xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink'";

    private static readonly Ink Red = Ink.Rgb(255, 0, 0);
    private static readonly Ink Blue = Ink.Rgb(0, 0, 255);

    private static Artwork Read(string body, string size = "width='100' height='100'") =>
        Artwork.FromSvg($"<svg {Namespaces} {size}>{body}</svg>");

    private static List<DrawOperation> Draw(string body, string size = "width='100' height='100'")
    {
        Artwork artwork = Read(body, size);
        return LayoutHarness.Draw(frame => frame.Artwork(artwork), artwork.Size).Operations;
    }

    private static List<PathOperation> Paths(string body, string size = "width='100' height='100'") =>
        Draw(body, size).OfType<PathOperation>().ToList();

    private static List<PathOperation> Painted(string body, string size = "width='100' height='100'") =>
        Paths(body, size).Where(operation => operation.Painting != PathPainting.Clip).ToList();

    private static Bounds Pixels(float left, float top, float right, float bottom) =>
        new Bounds(left * 0.75f, top * 0.75f, right * 0.75f, bottom * 0.75f);

    // ---- Size ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("width='200' height='100'", 150f, 75f)]
    [InlineData("width='2in' height='1in'", 144f, 72f)]
    [InlineData("viewBox='0 0 40 20'", 30f, 15f)]
    [InlineData("width='50%' viewBox='0 0 40 20'", 15f, 15f)]
    [InlineData("", 225f, 112.5f)]
    [InlineData("viewBox='0 0 0 20'", 225f, 112.5f)]
    [InlineData("viewBox='0 0 40 0'", 225f, 112.5f)]
    public void TheArtworkTakesTheSizeTheDocumentGivesItself(string size, float width, float height) =>
        Assert.Equal(new Extent(width, height), Read(string.Empty, size).Size);

    [Fact]
    public void ADocumentWithNoSizeIsRefused() =>
        Assert.Throws<FormatException>(() => Read(string.Empty, "width='0' height='10'"));

    [Fact]
    public void OnlyAnSvgIsRead()
    {
        Assert.Throws<FormatException>(() => Artwork.FromSvg("<html/>"));
        Assert.Throws<FormatException>(() => Artwork.FromSvg("<svg"));
        Assert.Throws<ArgumentNullException>(() => Artwork.FromSvg((string)null!));
        Assert.Throws<ArgumentNullException>(() => Artwork.FromSvg((Stream)null!));
    }

    [Fact]
    public void AStreamAndAFileReadAlike()
    {
        string svg = $"<svg {Namespaces} width='40' height='20'><rect width='10' height='10'/></svg>";
        string file = Path.Combine(Path.GetTempPath(), $"artwork-{Guid.NewGuid():N}.svg");
        File.WriteAllText(file, svg);

        try
        {
            using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));

            Assert.Equal(new Extent(30, 15), Artwork.FromSvg(stream).Size);
            Assert.Equal(new Extent(30, 15), Artwork.FromSvgFile(file).Size);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void EntitiesTheDocumentDeclaresAreExpanded()
    {
        Artwork artwork = Artwork.FromSvg(
            "<!DOCTYPE svg [<!ENTITY red 'red'>]>" +
            $"<svg {Namespaces} width='10' height='10'><rect width='10' height='10' fill='&red;'/></svg>");

        Assert.Equal(Red, LayoutHarness.Draw(frame => frame.Artwork(artwork), artwork.Size).Operations.OfType<PathOperation>().Single().Ink);
    }

    // ---- View boxes ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("xMidYMid meet", 25f, 0f, 75f, 50f)]
    [InlineData("xMinYMin meet", 0f, 0f, 50f, 50f)]
    [InlineData("xMaxYMax meet", 50f, 0f, 100f, 50f)]
    [InlineData("xMidYMid slice", 0f, -25f, 100f, 75f)]
    [InlineData("none", 0f, 0f, 100f, 50f)]
    public void AViewBoxIsFittedAsItsAspectRatioSays(string aspect, float left, float top, float right, float bottom)
    {
        PathOperation square = Painted(
            "<rect width='10' height='10'/>",
            $"width='100' height='50' viewBox='0 0 10 10' preserveAspectRatio='{aspect}'").Single();

        Approximately.Equal(Pixels(left, top, right, bottom), square.Bounds);
    }

    [Fact]
    public void AViewBoxMayStartAnywhere() =>
        Approximately.Equal(Pixels(0, 0, 50, 50), Painted("<rect x='10' y='10' width='5' height='5'/>", "width='100' height='100' viewBox='10 10 10 10'").Single().Bounds);

    [Fact]
    public void ANestedViewportIsPlacedAndClipped()
    {
        List<PathOperation> paths = Paths("<svg x='20' y='30' width='40' height='20' viewBox='0 0 4 2'><rect width='4' height='2'/></svg>");

        Assert.Equal(PathPainting.Clip, paths[0].Painting);
        Approximately.Equal(Pixels(20, 30, 60, 50), paths[1].Bounds);
    }

    // ---- Shapes ----------------------------------------------------------------------------------------------

    [Fact]
    public void EveryBasicShapeIsDrawn()
    {
        List<PathOperation> shapes = Painted(
            "<rect x='1' y='2' width='3' height='4'/>" +
            "<circle cx='50' cy='50' r='10'/>" +
            "<ellipse cx='50' cy='50' rx='20' ry='10'/>" +
            "<line x1='0' y1='0' x2='10' y2='10' stroke='red'/>" +
            "<polyline points='0,0 10,0 10,10' stroke='red'/>" +
            "<polygon points='0,0 10,0 10,10'/>" +
            "<path d='M0 0 L 5 5'/>");

        Assert.Equal(7, shapes.Count);
        Approximately.Equal(Pixels(1, 2, 4, 6), shapes[0].Bounds);
        Approximately.Equal(Pixels(40, 40, 60, 60), shapes[1].Bounds);
        Approximately.Equal(Pixels(30, 40, 70, 60), shapes[2].Bounds);
        Assert.Equal(PathPainting.Stroke, shapes[3].Painting);
        Assert.Equal(PathPainting.Stroke, shapes[4].Painting);
        Assert.Equal(PathVerb.Close, shapes[5].Path.Verbs.Last());
    }

    [Fact]
    public void ShapesOfNoSizeAreNotDrawn() =>
        Assert.Empty(Painted(
            "<rect width='0' height='5'/><rect width='5'/><circle r='0'/><ellipse rx='5'/><ellipse ry='5'/>" +
            "<polyline points='1 2'/><polygon/><path/><unknown/>"));

    [Theory]
    [InlineData("rx='5'", 5f, 5f)]
    [InlineData("ry='5'", 5f, 5f)]
    [InlineData("rx='2' ry='4'", 2f, 4f)]
    [InlineData("rx='50' ry='50'", 10f, 5f)]
    public void ARectanglesCornersAreRounded(string corners, float rx, float ry)
    {
        PathOperation rectangle = Painted($"<rect width='20' height='10' {corners}/>").Single();

        Assert.Contains(PathVerb.Cubic, rectangle.Path.Verbs);
        Assert.Equal(new Offset(rx, 0), rectangle.Path.Points[0]);
        Assert.Contains(new Offset(20, ry), rectangle.Path.Points);
    }

    [Fact]
    public void ANegativeCornerIsSquare() =>
        Assert.DoesNotContain(PathVerb.Cubic, Painted("<rect width='20' height='10' rx='-5'/>").Single().Path.Verbs);

    // ---- Painting --------------------------------------------------------------------------------------------

    [Fact]
    public void AShapeIsFilledBlackAndNotStrokedUnlessStyled()
    {
        PathOperation square = Painted("<rect width='5' height='5'/>").Single();

        Assert.Equal((PathPainting.Fill, Ink.Rgb(0, 0, 0), FillRule.NonZero), (square.Painting, square.Ink, square.Rule));
    }

    [Fact]
    public void FillsAndStrokesTakeTheirStyles()
    {
        List<PathOperation> painted = Painted(
            "<path d='M0 0 L5 5 L0 5 Z' fill='red' fill-rule='evenodd' fill-opacity='0.5' stroke='blue' stroke-width='3' " +
            "stroke-linecap='round' stroke-linejoin='bevel' stroke-miterlimit='7' stroke-dasharray='4 2' stroke-dashoffset='1' stroke-opacity='0.25'/>");

        Assert.Equal((Red.WithOpacity(0.5f), FillRule.EvenOdd), (painted[0].Ink, painted[0].Rule));
        Assert.Equal(Blue.WithOpacity(0.25f), painted[1].Ink);
        LineStyle style = painted[1].Style!.Value;
        Assert.Equal((3f, LineCap.Round, LineJoin.Bevel, 7f, 1f), (style.Weight, style.Cap, style.Join, style.MiterLimit, style.DashOffset));
        Assert.Equal([4f, 2f], style.Dashes!);
    }

    [Theory]
    [InlineData("stroke-linecap='square' stroke-linejoin='round'", LineCap.Square, LineJoin.Round)]
    [InlineData("stroke-linecap='butt' stroke-linejoin='miter'", LineCap.Butt, LineJoin.Miter)]
    public void CapsAndJoinsAreRead(string attributes, LineCap cap, LineJoin join)
    {
        LineStyle style = Painted($"<line x2='5' stroke='red' {attributes}/>").Single().Style!.Value;

        Assert.Equal((cap, join), (style.Cap, style.Join));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("4 -1")]
    [InlineData("x")]
    public void DashesThatCannotBeDrawnAreSolid(string dashes) =>
        Assert.Null(Painted($"<line x2='5' stroke='red' stroke-dasharray='{dashes}'/>").Single().Style!.Value.Dashes);

    [Fact]
    public void NothingIsPaintedThatIsNone()
    {
        Assert.Empty(Painted("<rect width='5' height='5' fill='none'/>"));
        Assert.Empty(Painted("<rect width='5' height='5' fill='none' stroke='red' stroke-width='0'/>"));
        Assert.Empty(Painted("<rect width='5' height='5' fill='bogus'/>"));
    }

    [Fact]
    public void OpacityIsCarriedDownToEveryFillAndStroke()
    {
        PathOperation square = Painted("<g opacity='0.5'><rect width='5' height='5' opacity='0.5' fill='red'/></g>").Single();

        Assert.Equal(0.25f, square.Ink.Opacity, 3);
    }

    [Theory]
    [InlineData("fill='red' opacity='5.7'")]
    [InlineData("fill='red' fill-opacity='5.7'")]
    [InlineData("fill='none' stroke='red' stroke-opacity='5.7'")]
    [InlineData("fill='url(#g)'")]
    public void AnOpacityBeyondOneIsOpaque(string attributes)
    {
        PathOperation square = Painted($"<linearGradient id='g'><stop stop-color='red' stop-opacity='5.7'/></linearGradient><rect width='5' height='5' {attributes}/>").Single();

        Assert.Equal(1f, square.Ink.Opacity);
    }

    [Fact]
    public void CurrentColourIsTheColourInForce()
    {
        Assert.Equal(Red, Painted("<g color='red'><rect width='5' height='5' fill='currentColor'/></g>").Single().Ink);
        Assert.Equal(Ink.Rgb(0, 0, 0), Painted("<rect width='5' height='5' fill='currentColor'/>").Single().Ink);
    }

    [Fact]
    public void HiddenAndUndisplayedContentIsNotDrawn()
    {
        Assert.Empty(Painted("<g display='none'><rect width='5' height='5'/></g>"));
        Assert.Empty(Painted("<g visibility='hidden'><rect width='5' height='5'/></g>"));
        Assert.Empty(Painted("<g visibility='collapse'><rect width='5' height='5'/></g>"));
        Assert.Single(Painted("<g visibility='hidden'><rect width='5' height='5' visibility='visible'/></g>"));
        Assert.Empty(Painted("<defs><rect width='5' height='5'/></defs><title>x</title><symbol><rect width='5' height='5'/></symbol>"));
    }

    // ---- Styles ----------------------------------------------------------------------------------------------

    [Fact]
    public void StylesComeFromAttributesThenStyleSheetsThenTheStyleAttribute()
    {
        List<PathOperation> painted = Painted(
            "<style>.a { fill: blue } #c { fill: green }</style>" +
            "<rect class='a' width='5' height='5' fill='red'/>" +
            "<rect class='a' width='5' height='5' style='fill: red'/>" +
            "<rect id='c' class='a' width='5' height='5'/>");

        Assert.Equal([Blue, Red, Ink.Rgb(0, 128, 0)], painted.Select(operation => operation.Ink));
    }

    [Fact]
    public void StylesAreInheritedUnlessSaidOtherwise()
    {
        List<PathOperation> painted = Painted(
            "<g fill='red' stroke='blue'><rect width='5' height='5'/><rect width='5' height='5' fill='inherit'/><rect width='5' height='5' fill='none' stroke='none'/></g>");

        Assert.Equal([Red, Blue, Red, Blue], painted.Select(operation => operation.Ink));
    }

    // ---- Transforms and clips --------------------------------------------------------------------------------

    [Fact]
    public void TransformsNestAndEndWithTheirElement()
    {
        List<PathOperation> painted = Painted(
            "<g transform='translate(10 20)'><rect width='5' height='5' transform='scale(2)'/></g><rect width='5' height='5'/>");

        Approximately.Equal(Pixels(10, 20, 20, 30), painted[0].Bounds);
        Approximately.Equal(Pixels(0, 0, 5, 5), painted[1].Bounds);
    }

    [Fact]
    public void AClipPathGathersItsShapesWithTheirTransforms()
    {
        List<PathOperation> paths = Paths(
            "<clipPath id='c' transform='translate(1 1)'><rect width='5' height='5'/><g transform='translate(10 0)'><circle r='2'/></g>" +
            "<use href='#dot' x='20'/><rect width='5' height='5' display='none'/><text>ignored</text></clipPath>" +
            "<circle id='dot' r='1' fill='none'/>" +
            "<rect width='50' height='50' clip-path='url(#c)' clip-rule='evenodd'/>");

        PathOperation clip = paths.Single(operation => operation.Painting == PathPainting.Clip);
        Assert.Equal(FillRule.EvenOdd, clip.Rule);
        Assert.Equal(3, clip.Path.Verbs.Count(verb => verb == PathVerb.Move));
        Assert.Contains(new Offset(1, 1), clip.Path.Points);
        Assert.Contains(new Offset(13, 1), clip.Path.Points);
        Assert.Contains(new Offset(22, 1), clip.Path.Points);
    }

    [Theory]
    [InlineData("url(#missing)")]
    [InlineData("url(#notclip)")]
    [InlineData("url(#box)")]
    [InlineData("none")]
    public void AClipThatCannotBeFoundOrFollowedIsNoClip(string reference)
    {
        List<PathOperation> paths = Paths(
            "<rect id='notclip' width='1' height='1' fill='none'/><clipPath id='box' clipPathUnits='objectBoundingBox'><rect width='1' height='1'/></clipPath>" +
            $"<rect width='5' height='5' clip-path='{reference}'/>");

        Assert.DoesNotContain(paths, operation => operation.Painting == PathPainting.Clip);
    }

    // ---- Gradients -------------------------------------------------------------------------------------------

    private static List<DrawOperation> Gradient(string gradient, string fill = "url(#g)") =>
        Draw($"<defs>{gradient}</defs><rect x='10' y='20' width='40' height='20' fill='{fill}'/>");

    [Fact]
    public void ALinearGradientFillsAcrossTheShapesBox()
    {
        List<DrawOperation> drawn = Gradient(
            "<linearGradient id='g' x1='0' y1='0' x2='100%' y2='0'><stop offset='0' stop-color='red'/><stop offset='0.5' style='stop-color: blue; stop-opacity: 0.5'/><stop offset='1' stop-color='red'/></linearGradient>");

        GradientOperation gradient = Assert.IsType<GradientOperation>(drawn[0]);
        Assert.Equal([0f, 0.5f, 1f], gradient.Gradient.Positions);
        Assert.Equal(Blue, gradient.Gradient.Inks[1].WithOpacity(1));
        Assert.Equal(0.833f, gradient.Gradient.Opacity, 3);
        (Offset start, Offset end) = gradient.Gradient.Axis(new Offset(10, 20), new Extent(40, 20));
        Assert.Equal((new Offset(10, 20), new Offset(50, 20)), (start, end));
    }

    [Fact]
    public void AGradientInUserSpaceKeepsItsOwnPoints()
    {
        GradientOperation gradient = Gradient(
            "<linearGradient id='g' gradientUnits='userSpaceOnUse' x1='5' y1='6' x2='70' y2='8' gradientTransform='translate(1 1)'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient>")
            .OfType<GradientOperation>().Single();

        (Offset start, Offset end) = gradient.Gradient.Axis(Offset.Zero, new Extent(1, 1));
        Assert.Equal((new Offset(6, 7), new Offset(71, 9)), (start, end));
    }

    [Fact]
    public void AGradientTakesStopsAndAttributesFromTheOneItRefersTo()
    {
        GradientOperation gradient = Gradient(
            "<linearGradient id='base' x2='0' y2='1'><stop offset='0.2' stop-color='red'/><stop offset='0.1' stop-color='blue'/></linearGradient>" +
            "<linearGradient id='g' xlink:href='#base'/>")
            .OfType<GradientOperation>().Single();

        // Stops out of order are held at the last position, as SVG says.
        Assert.Equal([0.2f, 0.2f], gradient.Gradient.Positions);
        (Offset start, Offset end) = gradient.Gradient.Axis(Offset.Zero, new Extent(10, 10));
        Assert.Equal((Offset.Zero, new Offset(0, 10)), (start, end));
    }

    [Fact]
    public void AGradientOfOneStopIsItsInk() =>
        Assert.Equal(Red, Gradient("<linearGradient id='g'><stop stop-color='red'/></linearGradient>").OfType<PathOperation>().Single().Ink);

    [Theory]
    [InlineData("<linearGradient id='g'/>", "url(#g)")]
    [InlineData("<linearGradient id='g'/>", "url(#g) blue")]
    [InlineData("<pattern id='g'/>", "url(#g) blue")]
    [InlineData("", "url(#g) blue")]
    [InlineData("", "url(#g")]
    public void AnUnusableGradientFallsBack(string gradient, string fill)
    {
        List<PathOperation> painted = Gradient(gradient, fill).OfType<PathOperation>().ToList();

        if (fill.EndsWith("blue", StringComparison.Ordinal))
            Assert.Equal(Blue, Assert.Single(painted).Ink);
        else
            Assert.Empty(painted);
    }

    [Fact]
    public void ARadialGradientIsTheMeanOfItsColours()
    {
        PathOperation painted = Gradient("<radialGradient id='g'><stop stop-color='red'/><stop offset='1' stop-color='blue' stop-opacity='0'/></radialGradient>")
            .OfType<PathOperation>().Single();

        Assert.Equal(Ink.Rgb(128, 0, 128).WithOpacity(0.5f), painted.Ink);
        Assert.Empty(Gradient("<radialGradient id='g'/>").OfType<PathOperation>());
    }

    [Fact]
    public void AStrokeCanBeAGradient()
    {
        List<DrawOperation> drawn = Draw(
            "<linearGradient id='g'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient>" +
            "<line x2='50' stroke='url(#g)' stroke-width='4'/>");

        Assert.IsType<GradientOperation>(drawn[0]);
        Assert.Equal(PathPainting.Stroke, Assert.IsType<PathOperation>(drawn[1]).Painting);
    }

    [Fact]
    public void AGradientReferringToItselfEnds() =>
        Assert.Empty(Gradient("<linearGradient id='g' href='#g'/>").OfType<PathOperation>());

    // ---- Use -------------------------------------------------------------------------------------------------

    [Fact]
    public void AUseDrawsWhatItRefersToWhereItSays()
    {
        List<PathOperation> painted = Painted(
            "<defs><rect id='r' width='5' height='5'/></defs><use href='#r' x='10' y='20' fill='red'/><use xlink:href='#r' fill='blue'/>");

        Assert.Equal([Red, Blue], painted.Select(operation => operation.Ink));
        Approximately.Equal(Pixels(10, 20, 15, 25), painted[0].Bounds);
    }

    [Fact]
    public void AUsedSymbolIsFittedToTheUse()
    {
        PathOperation star = Painted("<symbol id='s' viewBox='0 0 10 10'><rect width='10' height='10'/></symbol><use href='#s' width='40' height='40'/>").Single();

        Approximately.Equal(Pixels(0, 0, 40, 40), star.Bounds);
    }

    [Fact]
    public void AUseThatRefersToItselfOrNothingDrawsNothing()
    {
        Assert.Empty(Painted("<g id='loop'><use href='#loop'/></g>").Skip(0));
        Assert.Empty(Painted("<use href='#nowhere'/><use/><use href='elsewhere.svg#x'/>"));
    }

    // ---- Text ------------------------------------------------------------------------------------------------

    [Fact]
    public void TextIsSetInItsStyleAtItsPoint()
    {
        TextOperation text = Draw(
            "<text x='10 99' y='20' font-family=\"'Noto Sans', serif\" font-size='12' font-weight='700' font-style='italic' fill='red'> Hello\n   <tspan>there</tspan> </text>")
            .OfType<TextOperation>().Single();

        Assert.Equal("Hello there", text.Text);
        Assert.Equal(new Offset(7.5f, 15), text.Position);
        Assert.Equal(("Noto Sans", 12f, TypeWeight.Bold, true), (text.Style.Typeface, text.Style.PointSize, text.Style.Weight, text.Style.IsItalic));
        Assert.Equal(Red, text.Style.Ink);
    }

    [Theory]
    [InlineData("start", 10f)]
    [InlineData("middle", 8.5f)]
    [InlineData("end", 7f)]
    public void TextIsAnchoredAsItSays(string anchor, float x)
    {
        // The measurer used for tests gives every character half a point of width per point of size.
        TextOperation text = Draw($"<text x='10' y='20' font-size='1' text-anchor='{anchor}'>abcdef</text>").OfType<TextOperation>().Single();

        Assert.Equal(x * 0.75f, text.Position.X, 2);
    }

    [Theory]
    [InlineData("<text/>")]
    [InlineData("<text>  </text>")]
    [InlineData("<text fill='none'>x</text>")]
    [InlineData("<text visibility='hidden'>x</text>")]
    [InlineData("<text visibility='collapse'>x</text>")]
    public void TextWithNothingToShowIsNotSet(string text) =>
        Assert.Empty(Draw(text).OfType<TextOperation>());

    [Theory]
    [InlineData("font-weight='bold'", TypeWeight.Bold, false)]
    [InlineData("font-weight='bolder'", TypeWeight.Bold, false)]
    [InlineData("font-weight='600'", TypeWeight.Bold, false)]
    [InlineData("font-weight='500'", TypeWeight.Normal, false)]
    [InlineData("font-weight='lighter'", TypeWeight.Normal, false)]
    [InlineData("font-style='italic'", TypeWeight.Normal, true)]
    [InlineData("font-style='oblique'", TypeWeight.Normal, true)]
    [InlineData("font-style='normal'", TypeWeight.Normal, false)]
    public void TextIsBoldOrItalicAsItsStyleSays(string attributes, TypeWeight weight, bool italic)
    {
        TextOperation text = Draw($"<text {attributes}>x</text>").OfType<TextOperation>().Single();

        Assert.Equal((weight, italic), (text.Style.Weight, text.Style.IsItalic));
    }

    [Fact]
    public void TextIsSetInSansSerifByDefault()
    {
        TextOperation text = Draw("<text font-family=' , '>x</text><text font-weight='bolder'>y</text><text font-weight='300'>z</text>").OfType<TextOperation>().First();

        Assert.Equal("sans-serif", text.Style.Typeface);
    }

    // ---- Images ----------------------------------------------------------------------------------------------

    [Fact]
    public void AnImageCarriedInTheDocumentIsDrawn()
    {
        string png = Convert.ToBase64String(Rustaveli.Pdf.Images.PngWriter.Rgb(1, 1, [255, 0, 0]));
        ImageOperation image = Draw($"<image x='10' y='20' width='30' height='40' href='data:image/png;base64,{png}'/>").OfType<ImageOperation>().Single();

        Approximately.Equal(Pixels(10, 20, 40, 60), image.Bounds);
    }

    [Theory]
    [InlineData("href='picture.png' width='5' height='5'")]
    [InlineData("href='data:image/png;base64,!!!' width='5' height='5'")]
    [InlineData("href='data:image/png;base64,AAAA' width='5' height='5'")]
    [InlineData("href='data:image/png,raw' width='5' height='5'")]
    [InlineData("href='data:image/png' width='5' height='5'")]
    [InlineData("href='data:image/png;base64,AAAA' width='0' height='5'")]
    [InlineData("width='5' height='5'")]
    public void AnImageThatCannotBeReadHereIsLeftOut(string attributes) =>
        Assert.Empty(Draw($"<image {attributes}/>").OfType<ImageOperation>());

    // ---- Overflow --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("<circle cx='9e14' cy='5' r='9e14'/>")]
    [InlineData("<rect width='5' height='5' transform='scale(1e10) scale(1e10)'/>")]
    [InlineData("<clipPath id='c'><circle cx='9e14' cy='5' r='9e14'/></clipPath><rect width='5' height='5' clip-path='url(#c)'/>")]
    [InlineData("<linearGradient id='g' x2='1e14'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient><rect width='50' height='5' fill='url(#g)'/>")]
    [InlineData("<linearGradient id='g' x2='1e14'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient><rect width='50' height='5' fill='none' stroke='url(#g)'/>")]
    [InlineData("<svg width='10' height='10' viewBox='0 0 1e-44 1e-44'><rect width='5' height='5'/></svg>")]
    [InlineData("<symbol id='s' viewBox='0 0 1e-44 1e-44'><rect width='5' height='5'/></symbol><use href='#s' width='10' height='10'/>")]
    public void WhatReachesBeyondAPdfIsLeftOut(string body)
    {
        Assert.Empty(Painted(body));
        Assert.Single(Painted(body + "<rect width='5' height='5'/>"));
    }

    [Theory]
    [InlineData("0 0 1e-44 1e-44", "xMidYMid")]
    [InlineData("0 0 1e-44 10", "none")]
    [InlineData("0 0 10 1e-44", "none")]
    [InlineData("0 0 1e10 1e-10", "xMidYMid slice")]
    [InlineData("0 0 1e-10 1e10", "xMidYMid slice")]
    public void AViewBoxTooSmallToScaleDrawsNothing(string viewBox, string aspect)
    {
        Assert.Empty(Painted("<rect width='5' height='5'/>", $"width='100' height='100' viewBox='{viewBox}' preserveAspectRatio='{aspect}'"));
        Assert.Single(Painted("<rect width='5' height='5'/>", $"width='100' height='100' viewBox='0 0 10 10' preserveAspectRatio='{aspect}'"));
    }

    // ---- Depth -----------------------------------------------------------------------------------------------

    [Fact]
    public void NestingIsFollowedOnlySoDeep()
    {
        string deep = string.Concat(Enumerable.Repeat("<g>", 80)) + "<rect width='5' height='5'/>" + string.Concat(Enumerable.Repeat("</g>", 80));

        Assert.Empty(Painted(deep));
        Assert.Single(Painted("<g><g><rect width='5' height='5'/></g></g>"));
    }
}
