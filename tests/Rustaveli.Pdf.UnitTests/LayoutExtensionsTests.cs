using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Each chain wraps a 50x20 red block and is laid out in a 200x100 space, so every expected size and position
/// follows by hand from the method under test.
/// </summary>
public class LayoutExtensionsTests
{
    private static readonly Extent Space = new Extent(200, 100);

    private static Block Compose(Func<IFrame, IFrame> chain, float width = 50, float height = 20) =>
        LayoutHarness.Build(container => chain(container).Element(inner =>
            inner.Child = new FixedElement(width, height, TestInks.Red)));

    private static Extent Measure(Block root) => LayoutHarness.Measure(root, Space).Size;

    private static RectangleOperation Content(Block root) =>
        LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

    // Drawn at exactly the size it measured, as a parent that allots the natural size does (ADR 0012). Transforms
    // mirror and pivot across the box they are given, so this is the box their assertions describe.
    private static RectangleOperation ContentInItsOwnBox(Block root) =>
        LayoutHarness.Draw(root, Measure(root)).Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

    private static List<RectangleOperation> Bands(Block root, Ink color) =>
        LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().Where(r => r.Color == color).ToList();

    // ---- Padding -------------------------------------------------------------------------------------------

    [Fact]
    public void PaddingInsetsEverySide()
    {
        Block root = Compose(container => container.Inset(10));

        Approximately.Equal(new Extent(70, 40), Measure(root));
        Approximately.Equal(new Offset(10, 10), Content(root).Position);
    }

    [Fact]
    public void PaddingHorizontalInsetsTheLeftAndRight()
    {
        Block root = Compose(container => container.InsetHorizontal(10));

        Approximately.Equal(new Extent(70, 20), Measure(root));
        Approximately.Equal(new Offset(10, 0), Content(root).Position);
    }

    [Fact]
    public void PaddingVerticalInsetsTheTopAndBottom()
    {
        Block root = Compose(container => container.InsetVertical(10));

        Approximately.Equal(new Extent(50, 40), Measure(root));
        Approximately.Equal(new Offset(0, 10), Content(root).Position);
    }

    [Fact]
    public void PaddingLeftInsetsOnlyTheLeft()
    {
        Block root = Compose(container => container.InsetLeft(10));

        Approximately.Equal(new Extent(60, 20), Measure(root));
        Approximately.Equal(new Offset(10, 0), Content(root).Position);
    }

    [Fact]
    public void PaddingRightInsetsOnlyTheRight()
    {
        Block root = Compose(container => container.InsetRight(10));

        Approximately.Equal(new Extent(60, 20), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    [Fact]
    public void PaddingTopInsetsOnlyTheTop()
    {
        Block root = Compose(container => container.InsetTop(10));

        Approximately.Equal(new Extent(50, 30), Measure(root));
        Approximately.Equal(new Offset(0, 10), Content(root).Position);
    }

    [Fact]
    public void PaddingBottomInsetsOnlyTheBottom()
    {
        Block root = Compose(container => container.InsetBottom(10));

        Approximately.Equal(new Extent(50, 30), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    // ---- Painting ------------------------------------------------------------------------------------------

    // How far a background or border extends is the painting element's business, not the fluent API's: these
    // assert what was attached — colour, side, thickness, and that the content is still drawn — and never the
    // extent of the painted box.

    [Fact]
    public void BackgroundAcceptsHex()
    {
        Block root = Compose(container => container.Fill("#00FF00"));

        List<RectangleOperation> rectangles =
            LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().ToList();

        // Painted first, so it sits behind the content.
        Assert.Equal(2, rectangles.Count);
        Assert.Equal(Ink.Rgb(0, 255, 0), rectangles[0].Color);
        Assert.Equal(TestInks.Red, rectangles[1].Color);
    }

    [Fact]
    public void BorderLeftDrawsOnlyTheLeftBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.StrokeLeft(3)), TestInks.Black));

        Approximately.Equal(new Offset(0, 0), band.Position);
        Approximately.Equal(3f, band.Size.Width);
        Assert.True(band.Size.Height > band.Size.Width, "A left border is an upright band.");
    }

    [Fact]
    public void BorderRightDrawsOnlyTheRightBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.StrokeRight(3)), TestInks.Black));

        Approximately.Equal(0f, band.Position.Y);
        Approximately.Equal(3f, band.Size.Width);
        Assert.True(band.Position.X >= 47f, $"A right border belongs at the far edge, not at x = {band.Position.X}.");
    }

    [Fact]
    public void BorderTopDrawsOnlyTheTopBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.StrokeTop(3)), TestInks.Black));

        Approximately.Equal(new Offset(0, 0), band.Position);
        Approximately.Equal(3f, band.Size.Height);
        Assert.True(band.Size.Width > band.Size.Height, "A top border is a flat band.");
    }

    [Fact]
    public void BorderBottomDrawsOnlyTheBottomBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.StrokeBottom(3)), TestInks.Black));

        Approximately.Equal(0f, band.Position.X);
        Approximately.Equal(3f, band.Size.Height);
        Assert.True(band.Position.Y >= 17f, $"A bottom border belongs at the far edge, not at y = {band.Position.Y}.");
    }

    [Fact]
    public void BorderColorRecoloursTheBorderItFollows()
    {
        Block root = Compose(container => container.Stroke(2).StrokeInk(TestInks.Blue));

        Assert.Equal(4, Bands(root, TestInks.Blue).Count);
        Assert.Empty(Bands(root, TestInks.Black));
    }

    [Fact]
    public void BorderColorAcceptsHex()
    {
        Block root = Compose(container => container.Stroke(2).StrokeInk("#0000FF"));

        Assert.Equal(4, Bands(root, Ink.Rgb(0, 0, 255)).Count);
    }

    [Fact]
    public void BorderColorMustFollowABorder()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Compose(container => container.Inset(2).StrokeInk(TestInks.Blue)));

        Assert.Equal("BorderColor must be applied directly after a Border method.", exception.Message);
    }

    [Fact]
    public void CornerRadiusMustFollowABackgroundOrBorder()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Compose(container => container.Inset(2).RoundCorners(4)));

        Assert.Equal("CornerRadius must be applied directly after a Background or Border method.", exception.Message);
    }

    [Fact]
    public void CornerRadiusExplainsWhyAnUnevenBorderCannotBeRounded()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Compose(container => container.StrokeLeft(2).RoundCorners(4)));

        Assert.Equal(
            "CornerRadius requires a border of uniform width. Use Border(width) rather than a single-sided " +
            "BorderLeft/Right/Top/Bottom, and give it a width greater than zero.",
            exception.Message);
    }

    [Fact]
    public void AZeroCornerRadiusIsAcceptedOnAnUnevenBorder()
    {
        // Zero asks for square corners, which every border can draw, so there is nothing to refuse.
        Block root = Compose(container => container.StrokeLeft(2).RoundCorners(0));

        RecordedPage page = LayoutHarness.Draw(root, Space);

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Black);
    }

    // ---- Sizing --------------------------------------------------------------------------------------------

    [Fact]
    public void WidthPinsTheWidth()
    {
        Approximately.Equal(new Extent(80, 20), Measure(Compose(container => container.Width(80))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.Width(80), width: 120), Space).IsWrap);
    }

    [Fact]
    public void MinWidthGrowsNarrowContentButNotWideContent()
    {
        Approximately.Equal(new Extent(80, 20), Measure(Compose(container => container.MinWidth(80))));
        Approximately.Equal(new Extent(120, 20), Measure(Compose(container => container.MinWidth(80), width: 120)));
    }

    [Fact]
    public void MaxWidthCapsTheSpaceOfferedButDoesNotGrowContent()
    {
        Approximately.Equal(new Extent(50, 20), Measure(Compose(container => container.MaxWidth(80))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.MaxWidth(80), width: 120), Space).IsWrap);
    }

    [Fact]
    public void HeightPinsTheHeight()
    {
        Approximately.Equal(new Extent(50, 40), Measure(Compose(container => container.Height(40))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.Height(40), height: 60), Space).IsWrap);
    }

    [Fact]
    public void MinHeightGrowsShortContentButNotTallContent()
    {
        Approximately.Equal(new Extent(50, 40), Measure(Compose(container => container.MinHeight(40))));
        Approximately.Equal(new Extent(50, 60), Measure(Compose(container => container.MinHeight(40), height: 60)));
    }

    [Fact]
    public void MaxHeightCapsTheSpaceOfferedButDoesNotGrowContent()
    {
        Approximately.Equal(new Extent(50, 20), Measure(Compose(container => container.MaxHeight(40))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.MaxHeight(40), height: 60), Space).IsWrap);
    }

    [Fact]
    public void ExtendClaimsTheWholeSpace() =>
        Approximately.Equal(new Extent(200, 100), Measure(Compose(container => container.Expand())));

    [Fact]
    public void ExtendHorizontalClaimsOnlyTheWidth() =>
        Approximately.Equal(new Extent(200, 20), Measure(Compose(container => container.ExpandHorizontally())));

    [Fact]
    public void ExtendVerticalClaimsOnlyTheHeight() =>
        Approximately.Equal(new Extent(50, 100), Measure(Compose(container => container.ExpandVertically())));

    [Fact]
    public void AspectRatioDerivesTheHeightFromTheWidthByDefault() =>
        Approximately.Equal(new Extent(200, 50), Measure(Compose(container => container.Proportion(4))));

    [Fact]
    public void AspectRatioCanDeriveTheWidthFromTheHeight() =>
        Approximately.Equal(
            new Extent(50, 100),
            Measure(Compose(container => container.Proportion(0.5f, ProportionFit.FitHeight))));

    [Fact]
    public void ScaleToFitShrinksNoFurtherThanTheMinimumScale()
    {
        // A 400pt block needs half scale to fit 200pt. The default floor of a quarter allows that; a floor of
        // three quarters does not, so the block is passed through unscaled and cannot be placed.
        Block shrinkable = Compose(container => container.ShrinkToFit(), width: 400);
        Block barelyShrinkable = Compose(container => container.ShrinkToFit(0.75f), width: 400);

        Assert.True(LayoutHarness.Measure(shrinkable, Space).IsFullRender);
        Assert.True(LayoutHarness.Measure(barelyShrinkable, Space).IsWrap);
    }

    // ---- Flipping ------------------------------------------------------------------------------------------

    // A flip mirrors the block within its own box, so only the corner the drawing starts from moves.

    [Fact]
    public void FlipHorizontalStartsTheContentFromItsRightEdge()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(container => container.MirrorHorizontal()));

        Approximately.Equal(new Offset(50, 0), content.Position);
        Approximately.Equal(new Offset(0, 0), new Offset(content.Bounds.Left, content.Bounds.Top));
    }

    [Fact]
    public void FlipVerticalStartsTheContentFromItsBottomEdge()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(container => container.MirrorVertical()));

        Approximately.Equal(new Offset(0, 20), content.Position);
        Approximately.Equal(new Offset(0, 0), new Offset(content.Bounds.Left, content.Bounds.Top));
    }

    [Fact]
    public void FlipOverStartsTheContentFromTheOppositeCorner()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(container => container.MirrorBoth()));

        Approximately.Equal(new Offset(50, 20), content.Position);
        Approximately.Equal(new Offset(0, 0), new Offset(content.Bounds.Left, content.Bounds.Top));
    }

    // ---- Alignment -----------------------------------------------------------------------------------------

    [Fact]
    public void AlignLeftClaimsTheWidthAndKeepsContentAtTheLeft()
    {
        Block root = Compose(container => container.FlushLeft());

        Approximately.Equal(new Extent(200, 20), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    [Fact]
    public void AlignCenterCentresHorizontally() =>
        Approximately.Equal(new Offset(75, 0), Content(Compose(container => container.Centered())).Position);

    [Fact]
    public void AlignRightMovesContentToTheRight() =>
        Approximately.Equal(new Offset(150, 0), Content(Compose(container => container.FlushRight())).Position);

    [Fact]
    public void AlignTopClaimsTheHeightAndKeepsContentAtTheTop()
    {
        Block root = Compose(container => container.FlushTop());

        Approximately.Equal(new Extent(50, 100), Measure(root));
        Approximately.Equal(new Offset(0, 0), Content(root).Position);
    }

    [Fact]
    public void AlignMiddleCentresVertically() =>
        Approximately.Equal(new Offset(0, 40), Content(Compose(container => container.Middle())).Position);

    [Fact]
    public void AlignBottomMovesContentToTheBottom() =>
        Approximately.Equal(new Offset(0, 80), Content(Compose(container => container.FlushBottom())).Position);

    [Fact]
    public void AHorizontalAlignmentFoldsIntoAPrecedingVerticalOne()
    {
        Frame container = new Frame();

        IFrame vertical = container.FlushBottom();
        IFrame both = vertical.Centered();
        both.Element(inner => inner.Child = new FixedElement(50, 20, TestInks.Red));

        Assert.Same(vertical, both);
        Approximately.Equal(new Offset(75, 80), Content(container).Position);
    }

    [Fact]
    public void ALaterAlignmentOnTheSameAxisReplacesTheEarlierOne()
    {
        Frame container = new Frame();

        IFrame first = container.FlushLeft().FlushTop();
        IFrame second = first.FlushRight().FlushBottom();
        second.Element(inner => inner.Child = new FixedElement(50, 20, TestInks.Red));

        Assert.Same(first, second);
        Approximately.Equal(new Offset(150, 80), Content(container).Position);
    }

    [Fact]
    public void AnAlignerThatAlreadyHoldsContentIsNotRealigned()
    {
        // Folding into a filled aligner would silently move content composed earlier, so the call is treated as
        // new composition in an occupied slot and refused.
        Frame container = new Frame();
        IFrame aligned = container.FlushRight();
        aligned.Element(inner => inner.Child = new FixedElement(50, 20, TestInks.Red));

        Assert.Throws<CompositionException>(() => aligned.Centered());
        Approximately.Equal(new Offset(150, 0), Content(container).Position);
    }

    // ---- Transforms ----------------------------------------------------------------------------------------

    [Fact]
    public void TranslateXShiftsTheDrawingButNotTheLayout()
    {
        Block root = Compose(container => container.ShiftAcross(15));

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Approximately.Equal(new Offset(15, 0), Content(root).Position);
    }

    [Fact]
    public void TranslateYShiftsTheDrawingButNotTheLayout()
    {
        Block root = Compose(container => container.ShiftDown(15));

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Approximately.Equal(new Offset(0, 15), Content(root).Position);
    }

    [Fact]
    public void ScaleResizesBothAxesEqually()
    {
        Block root = Compose(container => container.Scale(0.5f));
        Bounds bounds = Content(root).Bounds;

        Approximately.Equal(new Extent(25, 10), Measure(root));
        Approximately.Equal(new Extent(25, 10), new Extent(bounds.Width, bounds.Height));
    }

    [Fact]
    public void ScaleResizesEachAxisIndependently()
    {
        Block root = Compose(container => container.Scale(0.5f, 2f));
        Bounds bounds = Content(root).Bounds;

        Approximately.Equal(new Extent(25, 40), Measure(root));
        Approximately.Equal(new Extent(25, 40), new Extent(bounds.Width, bounds.Height));
    }

    // Both rotations swap the axes; they differ in which corner the content is drawn from.

    [Fact]
    public void RotateRightTurnsAQuarterClockwise()
    {
        Block root = Compose(container => container.TurnRight());
        RectangleOperation content = ContentInItsOwnBox(root);

        Approximately.Equal(new Extent(20, 50), Measure(root));
        Approximately.Equal(new Offset(20, 0), content.Position);
        Approximately.Equal(new Extent(20, 50), new Extent(content.Bounds.Width, content.Bounds.Height));
    }

    [Fact]
    public void RotateLeftTurnsAQuarterAnticlockwise()
    {
        Block root = Compose(container => container.TurnLeft());
        RectangleOperation content = ContentInItsOwnBox(root);

        Approximately.Equal(new Extent(20, 50), Measure(root));
        Approximately.Equal(new Offset(0, 50), content.Position);
        Approximately.Equal(new Extent(20, 50), new Extent(content.Bounds.Width, content.Bounds.Height));
    }

    // ---- Flow control --------------------------------------------------------------------------------------

    [Fact]
    public void ShowIfTrueKeepsTheContent()
    {
        Block root = Compose(container => container.When(true));

        Approximately.Equal(new Extent(50, 20), Measure(root));
        Assert.Single(LayoutHarness.Draw(root, Space).Operations);
    }

    [Fact]
    public void ShowIfFalseRemovesTheContent()
    {
        Block root = Compose(container => container.When(false));

        Approximately.Equal(Extent.Zero, Measure(root));
        Assert.Empty(LayoutHarness.Draw(root, Space).Operations);
    }

    [Fact]
    public void ShowOnceDrawsTheContentOnlyTheFirstTime()
    {
        Block root = Compose(container => container.Once());

        Assert.Single(LayoutHarness.Draw(root, Space).Operations);
        Assert.Empty(LayoutHarness.Draw(root, Space).Operations);
    }

    // ---- Inherited context ---------------------------------------------------------------------------------

    [Fact]
    public void LeftToRightRestoresTheUsualDirectionInsideARightToLeftPassage()
    {
        Block nested = LayoutHarness.Build(container => container.RightToLeft().LeftToRight().Text("Hello"));
        Block outer = LayoutHarness.Build(container => container.RightToLeft().Text("Hello"));

        // Right-to-left text hugs the right edge: 200 less five 6pt characters.
        Approximately.Equal(0f, Assert.Single(LayoutHarness.Draw(nested, Space).Texts).Position.X);
        Approximately.Equal(170f, Assert.Single(LayoutHarness.Draw(outer, Space).Texts).Position.X);
    }

    [Fact]
    public void DefaultTextStyleRefusesAMissingRefinement()
    {
        Frame container = new Frame();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => container.DefaultType(null!));

        Assert.Equal("refinement", exception.ParamName);
        Assert.Null(container.Child);
    }

    // ---- Sizing escapes ------------------------------------------------------------------------------------

    [Fact]
    public void UnconstrainedContentOverflowsWhileReportingNoSize()
    {
        Block root = Compose(container => container.Unbounded(), width: 300);

        Approximately.Equal(Extent.Zero, Measure(root));
        Approximately.Equal(new Extent(300, 20), Content(root).Size);
    }

    // ---- Rules and placeholders ----------------------------------------------------------------------------

    [Fact]
    public void LineHorizontalDefaultsToAThinBlackRule()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.Rule()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Black, rule.Color);
        Approximately.Equal(new Extent(200, 1), rule.Size);
    }

    [Fact]
    public void LineHorizontalTakesAThicknessAndColour()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.Rule(3, TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, rule.Color);
        Approximately.Equal(new Extent(200, 3), rule.Size);
    }

    [Fact]
    public void LineVerticalDefaultsToAThinBlackRule()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.VerticalRule()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Black, rule.Color);
        Approximately.Equal(new Extent(1, 100), rule.Size);
    }

    [Fact]
    public void LineVerticalTakesAThicknessAndColour()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.VerticalRule(3, TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, rule.Color);
        Approximately.Equal(new Extent(3, 100), rule.Size);
    }

    [Fact]
    public void PlaceholderDefaultsToALightGrey()
    {
        RectangleOperation block = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.Placeholder()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.GreyLighten3, block.Color);
    }

    [Fact]
    public void PlaceholderTakesAColour()
    {
        RectangleOperation block = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.Placeholder(TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, block.Color);
    }

    // ---- Links ---------------------------------------------------------------------------------------------

    // Like the painting methods, these assert the target and that the content is still drawn, not the extent of
    // the clickable area.

    [Fact]
    public void HyperlinkMakesTheContentOpenTheUrl()
    {
        RecordedPage page = LayoutHarness.Draw(Compose(container => container.Link("https://example.com")), Space);

        Assert.Equal("https://example.com", Assert.Single(page.Operations.OfType<ExternalLinkOperation>()).Url);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void SectionNamesADestination()
    {
        RecordedPage page = LayoutHarness.Draw(Compose(container => container.Anchor("intro")), Space);

        Assert.Equal("intro", Assert.Single(page.Operations.OfType<DestinationOperation>()).Name);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void SectionLinkMakesTheContentJumpToTheSection()
    {
        RecordedPage page = LayoutHarness.Draw(Compose(container => container.CrossReference("intro")), Space);

        Assert.Equal("intro", Assert.Single(page.Operations.OfType<InternalLinkOperation>()).Destination);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LinkTargetsNameTheArgumentTheyReject(string? target)
    {
        Frame container = new Frame();

        Assert.Equal("url", Assert.ThrowsAny<ArgumentException>(() => container.Link(target!)).ParamName);
        Assert.Equal("name", Assert.ThrowsAny<ArgumentException>(() => container.Anchor(target!)).ParamName);
        Assert.Equal(
            "sectionName",
            Assert.ThrowsAny<ArgumentException>(() => container.CrossReference(target!)).ParamName);
        Assert.Null(container.Child);
    }
}
