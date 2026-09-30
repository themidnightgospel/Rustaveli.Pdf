namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Each chain wraps a 50x20 red block and is laid out in a 200x100 space, so every expected size and position
/// follows by hand from the method under test.
/// </summary>
public class FrameModifiersTests
{
    private static readonly Extent Space = new Extent(200, 100);

    private static Block Compose(Func<IFrame, IFrame> chain, float width = 50, float height = 20) =>
        LayoutHarness.Build(frame => chain(frame).Compose(inner =>
            inner.Slot().Child = new FixedBlock(width, height, TestInks.Red)));

    private static Extent Measure(Block root) => LayoutHarness.Plan(root, Space).Size;

    private static RectangleOperation Content(Block root) =>
        LayoutHarness.Render(root, Space).Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

    // Drawn at exactly the size it measured, as a parent that allots the natural size does (ADR 0012). Transforms
    // mirror and pivot across the box they are given, so this is the box their assertions describe.
    private static RectangleOperation ContentInItsOwnBox(Block root) =>
        LayoutHarness.Render(root, Measure(root)).Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

    private static List<RectangleOperation> Bands(Block root, Ink color) =>
        LayoutHarness.Render(root, Space).Operations.OfType<RectangleOperation>().Where(r => r.Ink == color).ToList();

    // ---- Padding -------------------------------------------------------------------------------------------

    [Fact]
    public void InsetAppliesToEverySide()
    {
        Block root = Compose(frame => frame.Inset(10));

        Approximately.Equal(new Extent(70, 40), Measure(root));
        Approximately.Equal(new Offset(10, 10), Content(root).Position);
    }

    [Fact]
    public void InsetHorizontalAppliesToTheLeftAndRight()
    {
        Block root = Compose(frame => frame.InsetHorizontal(10));

        Approximately.Equal(new Extent(70, 20), Measure(root));
        Approximately.Equal(new Offset(10, 0), Content(root).Position);
    }

    [Fact]
    public void InsetVerticalAppliesToTheTopAndBottom()
    {
        Block root = Compose(frame => frame.InsetVertical(10));

        Approximately.Equal(new Extent(50, 40), Measure(root));
        Approximately.Equal(new Offset(0, 10), Content(root).Position);
    }

    [Fact]
    public void InsetLeftAppliesOnlyToTheLeft()
    {
        Block root = Compose(frame => frame.InsetLeft(10));

        Approximately.Equal(new Extent(60, 20), Measure(root));
        Approximately.Equal(new Offset(10, 0), Content(root).Position);
    }

    [Fact]
    public void InsetRightAppliesOnlyToTheRight()
    {
        Block root = Compose(frame => frame.InsetRight(10));

        Approximately.Equal(new Extent(60, 20), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    [Fact]
    public void InsetTopAppliesOnlyToTheTop()
    {
        Block root = Compose(frame => frame.InsetTop(10));

        Approximately.Equal(new Extent(50, 30), Measure(root));
        Approximately.Equal(new Offset(0, 10), Content(root).Position);
    }

    [Fact]
    public void InsetBottomAppliesOnlyToTheBottom()
    {
        Block root = Compose(frame => frame.InsetBottom(10));

        Approximately.Equal(new Extent(50, 30), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    // ---- Painting ------------------------------------------------------------------------------------------

    // How far a background or border extends is the painting block's business, not the fluent API's: these
    // assert what was attached — colour, side, thickness, and that the content is still drawn — and never the
    // extent of the painted box.

    [Fact]
    public void FillAcceptsHex()
    {
        Block root = Compose(frame => frame.Fill("#00FF00"));

        List<RectangleOperation> rectangles =
            LayoutHarness.Render(root, Space).Operations.OfType<RectangleOperation>().ToList();

        // Painted first, so it sits behind the content.
        Assert.Equal(2, rectangles.Count);
        Assert.Equal(Ink.Rgb(0, 255, 0), rectangles[0].Ink);
        Assert.Equal(TestInks.Red, rectangles[1].Ink);
    }

    [Fact]
    public void StrokeLeftDrawsOnlyTheLeftBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(frame => frame.StrokeLeft(3)), TestInks.Black));

        Approximately.Equal(new Offset(0, 0), band.Position);
        Approximately.Equal(3f, band.Size.Width);
        Assert.True(band.Size.Height > band.Size.Width, "A left border is an upright band.");
    }

    [Fact]
    public void StrokeRightDrawsOnlyTheRightBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(frame => frame.StrokeRight(3)), TestInks.Black));

        Approximately.Equal(0f, band.Position.Y);
        Approximately.Equal(3f, band.Size.Width);
        Assert.True(band.Position.X >= 47f, $"A right border belongs at the far edge, not at x = {band.Position.X}.");
    }

    [Fact]
    public void StrokeTopDrawsOnlyTheTopBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(frame => frame.StrokeTop(3)), TestInks.Black));

        Approximately.Equal(new Offset(0, 0), band.Position);
        Approximately.Equal(3f, band.Size.Height);
        Assert.True(band.Size.Width > band.Size.Height, "A top border is a flat band.");
    }

    [Fact]
    public void StrokeBottomDrawsOnlyTheBottomBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(frame => frame.StrokeBottom(3)), TestInks.Black));

        Approximately.Equal(0f, band.Position.X);
        Approximately.Equal(3f, band.Size.Height);
        Assert.True(band.Position.Y >= 17f, $"A bottom border belongs at the far edge, not at y = {band.Position.Y}.");
    }

    [Fact]
    public void StrokeInkRecoloursTheStrokeItFollows()
    {
        Block root = Compose(frame => frame.Stroke(2).StrokeInk(TestInks.Blue));

        Assert.Equal(4, Bands(root, TestInks.Blue).Count);
        Assert.Empty(Bands(root, TestInks.Black));
    }

    [Fact]
    public void StrokeInkAcceptsHex()
    {
        Block root = Compose(frame => frame.Stroke(2).StrokeInk("#0000FF"));

        Assert.Equal(4, Bands(root, Ink.Rgb(0, 0, 255)).Count);
    }

    [Fact]
    public void StrokeInkMustFollowAStroke()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() =>
            Compose(frame => frame.Inset(2).StrokeInk(TestInks.Blue)));

        Assert.Equal("StrokeInk must directly follow Stroke, StrokeLeft, StrokeTop, StrokeRight or StrokeBottom.", exception.Message);
    }

    [Fact]
    public void RoundCornersMustFollowAFillOrStroke()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() =>
            Compose(frame => frame.Inset(2).RoundCorners(4)));

        Assert.Equal("RoundCorners must directly follow Fill, DropShadow or a Stroke method.", exception.Message);
    }

    [Fact]
    public void RoundCornersExplainsWhyAnUnevenStrokeCannotBeRounded()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() =>
            Compose(frame => frame.StrokeLeft(2).RoundCorners(4)));

        Assert.Equal(
            "RoundCorners needs a stroke of one weight on every side, greater than zero. Use Stroke(weight) " +
            "rather than StrokeLeft, StrokeTop, StrokeRight or StrokeBottom.",
            exception.Message);
    }

    [Fact]
    public void AZeroRadiusIsAcceptedOnAnUnevenStroke()
    {
        // Zero asks for square corners, which every border can draw, so there is nothing to refuse.
        Block root = Compose(frame => frame.StrokeLeft(2).RoundCorners(0));

        RecordedPage page = LayoutHarness.Render(root, Space);

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Black);
    }

    // ---- Sizing --------------------------------------------------------------------------------------------

    [Fact]
    public void WidthPinsTheWidth()
    {
        Approximately.Equal(new Extent(80, 20), Measure(Compose(frame => frame.Width(80))));
        Assert.True(LayoutHarness.Plan(Compose(frame => frame.Width(80), width: 120), Space).IsDeferred);
    }

    [Fact]
    public void MinWidthGrowsNarrowContentButNotWideContent()
    {
        Approximately.Equal(new Extent(80, 20), Measure(Compose(frame => frame.MinWidth(80))));
        Approximately.Equal(new Extent(120, 20), Measure(Compose(frame => frame.MinWidth(80), width: 120)));
    }

    [Fact]
    public void MaxWidthCapsTheSpaceOfferedButDoesNotGrowContent()
    {
        Approximately.Equal(new Extent(50, 20), Measure(Compose(frame => frame.MaxWidth(80))));
        Assert.True(LayoutHarness.Plan(Compose(frame => frame.MaxWidth(80), width: 120), Space).IsDeferred);
    }

    [Fact]
    public void HeightPinsTheHeight()
    {
        Approximately.Equal(new Extent(50, 40), Measure(Compose(frame => frame.Height(40))));
        Assert.True(LayoutHarness.Plan(Compose(frame => frame.Height(40), height: 60), Space).IsDeferred);
    }

    [Fact]
    public void MinHeightGrowsShortContentButNotTallContent()
    {
        Approximately.Equal(new Extent(50, 40), Measure(Compose(frame => frame.MinHeight(40))));
        Approximately.Equal(new Extent(50, 60), Measure(Compose(frame => frame.MinHeight(40), height: 60)));
    }

    [Fact]
    public void MaxHeightCapsTheSpaceOfferedButDoesNotGrowContent()
    {
        Approximately.Equal(new Extent(50, 20), Measure(Compose(frame => frame.MaxHeight(40))));
        Assert.True(LayoutHarness.Plan(Compose(frame => frame.MaxHeight(40), height: 60), Space).IsDeferred);
    }

    [Fact]
    public void ExpandClaimsTheWholeSpace() =>
        Approximately.Equal(new Extent(200, 100), Measure(Compose(frame => frame.Expand())));

    [Fact]
    public void ExpandHorizontallyClaimsOnlyTheWidth() =>
        Approximately.Equal(new Extent(200, 20), Measure(Compose(frame => frame.ExpandHorizontally())));

    [Fact]
    public void ExpandVerticallyClaimsOnlyTheHeight() =>
        Approximately.Equal(new Extent(50, 100), Measure(Compose(frame => frame.ExpandVertically())));

    [Fact]
    public void ProportionDerivesTheHeightFromTheWidthByDefault() =>
        Approximately.Equal(new Extent(200, 50), Measure(Compose(frame => frame.Proportion(4))));

    [Fact]
    public void ProportionCanDeriveTheWidthFromTheHeight() =>
        Approximately.Equal(
            new Extent(50, 100),
            Measure(Compose(frame => frame.Proportion(0.5f, ProportionFit.Height))));

    [Fact]
    public void ShrinkToFitShrinksNoFurtherThanTheMinimumScale()
    {
        // A 400pt block needs half scale to fit 200pt. The default floor of a quarter allows that; a floor of
        // three quarters does not, so the block is passed through unscaled and cannot be placed.
        Block shrinkable = Compose(frame => frame.ShrinkToFit(), width: 400);
        Block barelyShrinkable = Compose(frame => frame.ShrinkToFit(0.75f), width: 400);

        Assert.True(LayoutHarness.Plan(shrinkable, Space).IsComplete);
        Assert.True(LayoutHarness.Plan(barelyShrinkable, Space).IsDeferred);
    }

    // ---- Flipping ------------------------------------------------------------------------------------------

    // A flip mirrors the block within its own box, so only the corner the drawing starts from moves.

    [Fact]
    public void MirrorHorizontalStartsTheContentFromItsRightEdge()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(frame => frame.MirrorHorizontal()));

        Approximately.Equal(new Offset(50, 0), content.Position);
        Approximately.Equal(new Offset(0, 0), new Offset(content.Bounds.Left, content.Bounds.Top));
    }

    [Fact]
    public void MirrorVerticalStartsTheContentFromItsBottomEdge()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(frame => frame.MirrorVertical()));

        Approximately.Equal(new Offset(0, 20), content.Position);
        Approximately.Equal(new Offset(0, 0), new Offset(content.Bounds.Left, content.Bounds.Top));
    }

    [Fact]
    public void MirrorBothStartsTheContentFromTheOppositeCorner()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(frame => frame.MirrorBoth()));

        Approximately.Equal(new Offset(50, 20), content.Position);
        Approximately.Equal(new Offset(0, 0), new Offset(content.Bounds.Left, content.Bounds.Top));
    }

    // ---- Alignment -----------------------------------------------------------------------------------------

    [Fact]
    public void FlushLeftMeasuresAsItsContentAndKeepsItAtTheLeft()
    {
        Block root = Compose(frame => frame.FlushLeft());

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    [Fact]
    public void CenteredCentresHorizontally() =>
        Approximately.Equal(new Offset(75, 0), Content(Compose(frame => frame.Centered())).Position);

    [Fact]
    public void FlushRightMovesContentToTheRight() =>
        Approximately.Equal(new Offset(150, 0), Content(Compose(frame => frame.FlushRight())).Position);

    [Fact]
    public void FlushTopMeasuresAsItsContentAndKeepsItAtTheTop()
    {
        Block root = Compose(frame => frame.FlushTop());

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    [Fact]
    public void MiddleCentresVertically() =>
        Approximately.Equal(new Offset(0, 40), Content(Compose(frame => frame.Middle())).Position);

    [Fact]
    public void FlushBottomMovesContentToTheBottom() =>
        Approximately.Equal(new Offset(0, 80), Content(Compose(frame => frame.FlushBottom())).Position);

    [Fact]
    public void AHorizontalAlignmentFoldsIntoAPrecedingVerticalOne()
    {
        Frame frame = new Frame();

        IFrame vertical = frame.FlushBottom();
        IFrame both = vertical.Centered();
        both.Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.Red));

        Assert.Same(vertical, both);
        Approximately.Equal(new Offset(75, 80), Content(frame).Position);
    }

    [Fact]
    public void ALaterAlignmentOnTheSameAxisReplacesTheEarlierOne()
    {
        Frame frame = new Frame();

        IFrame first = frame.FlushLeft().FlushTop();
        IFrame second = first.FlushRight().FlushBottom();
        second.Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.Red));

        Assert.Same(first, second);
        Approximately.Equal(new Offset(150, 80), Content(frame).Position);
    }

    [Fact]
    public void AnAlignerThatAlreadyHoldsContentIsNotRealigned()
    {
        // Folding into a filled aligner would silently move content composed earlier, so the call is treated as
        // new composition in an occupied slot and refused.
        Frame frame = new Frame();
        IFrame aligned = frame.FlushRight();
        aligned.Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.Red));

        Assert.Throws<CompositionException>(() => aligned.Centered());
        Approximately.Equal(new Offset(150, 0), Content(frame).Position);
    }

    // ---- Transforms ----------------------------------------------------------------------------------------

    [Fact]
    public void ShiftingAcrossMovesTheDrawingButNotTheLayout()
    {
        Block root = Compose(frame => frame.ShiftAcross(15));

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Approximately.Equal(new Offset(15, 0), Content(root).Position);
    }

    [Fact]
    public void ShiftingDownMovesTheDrawingButNotTheLayout()
    {
        Block root = Compose(frame => frame.ShiftDown(15));

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Approximately.Equal(new Offset(0, 15), Content(root).Position);
    }

    [Fact]
    public void ScaleResizesBothAxesEqually()
    {
        Block root = Compose(frame => frame.Scale(0.5f));
        Bounds bounds = Content(root).Bounds;

        Approximately.Equal(new Extent(25, 10), Measure(root));
        Approximately.Equal(new Extent(25, 10), new Extent(bounds.Width, bounds.Height));
    }

    [Fact]
    public void ScaleResizesEachAxisIndependently()
    {
        Block root = Compose(frame => frame.Scale(0.5f, 2f));
        Bounds bounds = Content(root).Bounds;

        Approximately.Equal(new Extent(25, 40), Measure(root));
        Approximately.Equal(new Extent(25, 40), new Extent(bounds.Width, bounds.Height));
    }

    // Both rotations swap the axes; they differ in which corner the content is drawn from.

    [Fact]
    public void TurnRightTurnsAQuarterClockwise()
    {
        Block root = Compose(frame => frame.TurnRight());
        RectangleOperation content = ContentInItsOwnBox(root);

        Approximately.Equal(new Extent(20, 50), Measure(root));
        Approximately.Equal(new Offset(20, 0), content.Position);
        Approximately.Equal(new Extent(20, 50), new Extent(content.Bounds.Width, content.Bounds.Height));
    }

    [Fact]
    public void TurnLeftTurnsAQuarterAnticlockwise()
    {
        Block root = Compose(frame => frame.TurnLeft());
        RectangleOperation content = ContentInItsOwnBox(root);

        Approximately.Equal(new Extent(20, 50), Measure(root));
        Approximately.Equal(new Offset(0, 50), content.Position);
        Approximately.Equal(new Extent(20, 50), new Extent(content.Bounds.Width, content.Bounds.Height));
    }

    [Fact]
    public void RotateTurnsAboutTheCentreWithoutChangingTheLayout()
    {
        Block root = Compose(frame => frame.Rotate(180));
        RectangleOperation content = ContentInItsOwnBox(root);

        // A half turn about the centre lands the content back over its own box, drawn from the far corner.
        Approximately.Equal(new Extent(50, 20), Measure(root));
        Approximately.Equal(new Offset(50, 20), content.Position);
        Approximately.Equal(new Offset(0, 0), new Offset(content.Bounds.Left, content.Bounds.Top));
    }

    // ---- Flow control --------------------------------------------------------------------------------------

    [Fact]
    public void WhenTrueKeepsTheContent()
    {
        Block root = Compose(frame => frame.When(true));

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Assert.Single(LayoutHarness.Render(root, Space).Operations);
    }

    [Fact]
    public void WhenFalseRemovesTheContent()
    {
        Block root = Compose(frame => frame.When(false));

        Approximately.Equal(Extent.Zero, Measure(root));
        Assert.Empty(LayoutHarness.Render(root, Space).Operations);
    }

    [Fact]
    public void OnceDrawsTheContentOnlyTheFirstTime()
    {
        Block root = Compose(frame => frame.Once());

        Assert.Single(LayoutHarness.Render(root, Space).Operations);
        Assert.Empty(LayoutHarness.Render(root, Space).Operations);
    }

    // ---- Inherited context ---------------------------------------------------------------------------------

    [Fact]
    public void LeftToRightRestoresTheUsualDirectionInsideARightToLeftPassage()
    {
        Block nested = LayoutHarness.Build(frame => frame.RightToLeft().LeftToRight().Text("Hello"));
        Block outer = LayoutHarness.Build(frame => frame.RightToLeft().Text("Hello"));

        // Right-to-left text hugs the right edge: 200 less five 6pt characters.
        Approximately.Equal(0f, Assert.Single(LayoutHarness.Render(nested, Space).Texts).Position.X);
        Approximately.Equal(170f, Assert.Single(LayoutHarness.Render(outer, Space).Texts).Position.X);
    }

    [Fact]
    public void DefaultTypeRefusesAMissingRefinement()
    {
        Frame frame = new Frame();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => frame.DefaultType(null!));

        Assert.Equal("refinement", exception.ParamName);
        Assert.Null(frame.Slot().Child);
    }

    // ---- Sizing escapes ------------------------------------------------------------------------------------

    [Fact]
    public void UnboundedContentOverflowsWhileReportingNoSize()
    {
        Block root = Compose(frame => frame.Unbounded(), width: 300);

        Approximately.Equal(Extent.Zero, Measure(root));
        Approximately.Equal(new Extent(300, 20), Content(root).Size);
    }

    // ---- Rules and placeholders ----------------------------------------------------------------------------

    [Fact]
    public void LineHorizontalDefaultsToAThinBlackRule()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Render(LayoutHarness.Build(frame => frame.Rule()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Black, rule.Ink);
        Approximately.Equal(new Extent(200, 1), rule.Size);
    }

    [Fact]
    public void LineHorizontalTakesAThicknessAndColour()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Render(LayoutHarness.Build(frame => frame.Rule(3, TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, rule.Ink);
        Approximately.Equal(new Extent(200, 3), rule.Size);
    }

    [Fact]
    public void LineVerticalDefaultsToAThinBlackRule()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Render(LayoutHarness.Build(frame => frame.VerticalRule()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Black, rule.Ink);
        Approximately.Equal(new Extent(1, 100), rule.Size);
    }

    [Fact]
    public void LineVerticalTakesAThicknessAndColour()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Render(LayoutHarness.Build(frame => frame.VerticalRule(3, TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, rule.Ink);
        Approximately.Equal(new Extent(3, 100), rule.Size);
    }

    [Fact]
    public void PlaceholderDefaultsToALightGrey()
    {
        RectangleOperation block = Assert.Single(
            LayoutHarness.Render(LayoutHarness.Build(frame => frame.Placeholder()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.GreyLighten3, block.Ink);
    }

    [Fact]
    public void PlaceholderTakesAColour()
    {
        RectangleOperation block = Assert.Single(
            LayoutHarness.Render(LayoutHarness.Build(frame => frame.Placeholder(TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, block.Ink);
    }

    [Fact]
    public void APlaceholderSaysWhatWillGoThereCentredInGrey()
    {
        RecordedPage page = LayoutHarness.Render(LayoutHarness.Build(frame => frame.Placeholder("Logo", TestInks.Red)), Space);

        Assert.Equal((Ink)TestInks.Red, Assert.Single(page.Operations.OfType<RectangleOperation>()).Ink);

        TextOperation label = Assert.Single(page.Operations.OfType<TextOperation>());
        Assert.Equal("Logo", label.Text);
        Assert.Equal(Ink.Rgb(0x75, 0x75, 0x75), label.Style.Ink);

        // "Logo" is four characters at half the size each: 24 wide at 12 points, centred in the 200 by 100 space.
        Approximately.Equal(88f, label.Position.X);
        Assert.InRange(label.Position.Y, 45f, 55f);
    }

    [Fact]
    public void APlaceholderTooSmallForItsLabelShowsTheBoxAlone()
    {
        RecordedPage page = LayoutHarness.Render(LayoutHarness.Build(frame => frame.Placeholder("Logo")), new Extent(200, 1));

        Assert.Single(page.Operations.OfType<RectangleOperation>());
        Assert.Empty(page.Operations.OfType<TextOperation>());
    }

    [Fact]
    public void APlaceholderShowsItsLabelOnEveryPageItIsDrawnOn()
    {
        PlaceholderBlock block = new PlaceholderBlock("Chart");
        RenderContext context = new RenderContext(new RecordingSurface(), LayoutHarness.Context());

        Assert.Equal("Chart", block.Label);
        Assert.Null(new PlaceholderBlock().Label);
        Assert.Single(block.GetChildren());

        RecordingSurface surface = (RecordingSurface)context.Surface;
        surface.BeginPage(Space);
        block.Render(Space, context);
        block.Render(Space, context);
        surface.EndPage();

        Assert.Equal(2, surface.Pages[0].Operations.OfType<TextOperation>().Count());
    }

    [Fact]
    public void APlaceholderLabelIsNeverNull() =>
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Placeholder((string)null!)));

    // ---- Links ---------------------------------------------------------------------------------------------

    // Like the painting methods, these assert the target and that the content is still drawn, not the extent of
    // the clickable area.

    [Fact]
    public void LinkMakesTheContentOpenTheUrl()
    {
        RecordedPage page = LayoutHarness.Render(Compose(frame => frame.Link("https://example.com")), Space);

        Assert.Equal("https://example.com", Assert.Single(page.Operations.OfType<ExternalLinkOperation>()).Url);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void SectionNamesADestination()
    {
        RecordedPage page = LayoutHarness.Render(Compose(frame => frame.Anchor("intro")), Space);

        Assert.Equal("intro", Assert.Single(page.Operations.OfType<DestinationOperation>()).Name);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void CrossReferenceMakesTheContentJumpToTheAnchor()
    {
        RecordedPage page = LayoutHarness.Render(Compose(frame => frame.CrossReference("intro")), Space);

        Assert.Equal("intro", Assert.Single(page.Operations.OfType<InternalLinkOperation>()).Destination);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LinkTargetsNameTheArgumentTheyReject(string? target)
    {
        Frame frame = new Frame();

        Assert.Equal("url", Assert.ThrowsAny<ArgumentException>(() => frame.Link(target!)).ParamName);
        Assert.Equal("name", Assert.ThrowsAny<ArgumentException>(() => frame.Anchor(target!)).ParamName);
        Assert.Equal("anchor", Assert.ThrowsAny<ArgumentException>(() => frame.CrossReference(target!)).ParamName);
        Assert.Null(frame.Slot().Child);
    }

    // ---- Numbers ---------------------------------------------------------------------------------------------

    public static TheoryData<string, Action<IFrame>> NumbersThatCannotBeLaidOut => new()
    {
        { "value", frame => frame.Inset(float.NaN) },
        { "value", frame => frame.InsetHorizontal(float.PositiveInfinity) },
        { "value", frame => frame.InsetVertical(2e15f) },
        { "value", frame => frame.InsetLeft(float.NaN) },
        { "value", frame => frame.InsetRight(float.NaN) },
        { "value", frame => frame.InsetTop(float.NaN) },
        { "value", frame => frame.InsetBottom(float.NegativeInfinity) },
        { "weight", frame => frame.Stroke(-1) },
        { "weight", frame => frame.StrokeLeft(float.NaN) },
        { "weight", frame => frame.StrokeRight(float.NaN) },
        { "weight", frame => frame.StrokeTop(float.NaN) },
        { "weight", frame => frame.StrokeBottom(float.PositiveInfinity) },
        { "value", frame => frame.Width(-1) },
        { "value", frame => frame.MinWidth(float.NaN) },
        { "value", frame => frame.MaxWidth(float.NaN) },
        { "value", frame => frame.Height(float.NaN) },
        { "value", frame => frame.MinHeight(-5) },
        { "value", frame => frame.MaxHeight(float.PositiveInfinity) },
        { "ratio", frame => frame.Proportion(float.NaN) },
        { "ratio", frame => frame.Proportion(0) },
        { "ratio", frame => frame.Proportion(-2) },
        { "minScale", frame => frame.ShrinkToFit(float.NaN) },
        { "minScale", frame => frame.ShrinkToFit(0) },
        { "minScale", frame => frame.ShrinkToFit(1.5f) },
        { "value", frame => frame.ShiftAcross(float.NaN) },
        { "value", frame => frame.ShiftDown(float.PositiveInfinity) },
        { "factor", frame => frame.Scale(0) },
        { "factor", frame => frame.Scale(float.NaN) },
        { "scaleX", frame => frame.Scale(float.NaN, 1) },
        { "scaleY", frame => frame.Scale(1, 0) },
        { "degrees", frame => frame.Rotate(float.NaN) },
        { "minHeight", frame => frame.RequireSpace(-1) },
        { "minHeight", frame => frame.RequireSpace(float.NaN) },
        { "weight", frame => frame.Rule(-1) },
        { "weight", frame => frame.Rule(float.NaN, TestInks.Red, [2f, 1f]) },
        { "weight", frame => frame.Rule(float.NaN, Gradient.Across(TestInks.Red, TestInks.Blue)) },
        { "weight", frame => frame.Rule(float.NaN, Gradient.Across(TestInks.Red, TestInks.Blue), [2f, 1f]) },
        { "weight", frame => frame.VerticalRule(float.PositiveInfinity) },
        { "weight", frame => frame.VerticalRule(-1, TestInks.Red, [2f, 1f]) },
        { "weight", frame => frame.VerticalRule(float.NaN, Gradient.Across(TestInks.Red, TestInks.Blue)) },
        { "weight", frame => frame.VerticalRule(float.NaN, Gradient.Across(TestInks.Red, TestInks.Blue), [2f, 1f]) },
        { "value", frame => frame.Stack(stack => stack.SpaceBetween(float.NaN)) },
        { "value", frame => frame.Columns(columns => columns.Gutter(-1)) },
        { "weight", frame => frame.Columns(columns => columns.Share(float.NaN)) },
        { "width", frame => frame.Columns(columns => columns.Fixed(-1)) },
        { "value", frame => frame.Flow(flow => flow.Gutter(float.NaN)) },
        { "value", frame => frame.Flow(flow => flow.SpaceBetweenLines(-1)) },
        { "value", frame => frame.Grid(grid => grid.Gutter(float.NaN)) },
        { "value", frame => frame.Grid(grid => grid.SpaceBetweenRows(float.PositiveInfinity)) },
        { "width", frame => frame.List(list => list.MarkerIndent(float.NaN)) },
        { "value", frame => frame.List(list => list.SpaceBetween(-1)) },
        { "value", frame => frame.FlowColumns(columns => columns.Gutter(float.NaN)) },
        { "indent", frame => frame.Text(text => text.FirstLineIndent(float.NaN)) },
        { "spacing", frame => frame.Text(text => text.SpaceBetweenParagraphs(-1)) },
        { "weight", frame => frame.Table(table => table.Columns(columns => columns.Share(-1))) },
        { "width", frame => frame.Table(table => table.Columns(columns => columns.Fixed(float.NaN))) },
    };

    [Theory]
    [MemberData(nameof(NumbersThatCannotBeLaidOut))]
    public void ANumberThatCannotBeLaidOutIsRefusedWhereItIsGiven(string parameter, Action<IFrame> compose)
    {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(() => LayoutHarness.Build(frame => compose(frame)));

        Assert.Equal(parameter, refused.ParamName);
    }

    [Fact]
    public void NumbersThatCanBeLaidOutAreAccepted()
    {
        Block root = LayoutHarness.Build(frame => frame
            .Inset(-5).ShiftAcross(-3).ShiftDown(-2).Rotate(-45).Scale(-1, 2).Scale(0.5f).Width(0).MinHeight(0)
            .Proportion(0.5f).ShrinkToFit(1).RequireSpace(0).Stroke(0)
            .Stack(stack =>
            {
                stack.SpaceBetween(0);
                stack.Add().Rule(0);
                stack.Add().VerticalRule(0);
            }));

        Assert.NotNull(root);
    }
}
