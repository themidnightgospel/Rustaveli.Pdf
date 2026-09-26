using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Each chain wraps a 50x20 red block and is laid out in a 200x100 space, so every expected size and position
/// follows by hand from the method under test.
/// </summary>
public class LayoutExtensionsTests
{
    private static readonly Size Space = new Size(200, 100);

    private static Element Compose(Func<IContainer, IContainer> chain, float width = 50, float height = 20) =>
        LayoutHarness.Build(container => chain(container).Element(inner =>
            inner.Child = new FixedElement(width, height, TestInks.Red)));

    private static Size Measure(Element root) => LayoutHarness.Measure(root, Space).Size;

    private static RectangleOperation Content(Element root) =>
        LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

    // Drawn at exactly the size it measured, as a parent that allots the natural size does (ADR 0012). Transforms
    // mirror and pivot across the box they are given, so this is the box their assertions describe.
    private static RectangleOperation ContentInItsOwnBox(Element root) =>
        LayoutHarness.Draw(root, Measure(root)).Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

    private static List<RectangleOperation> Bands(Element root, Ink color) =>
        LayoutHarness.Draw(root, Space).Operations.OfType<RectangleOperation>().Where(r => r.Color == color).ToList();

    // ---- Padding -------------------------------------------------------------------------------------------

    [Fact]
    public void PaddingInsetsEverySide()
    {
        Element root = Compose(container => container.Padding(10));

        Approximately.Equal(new Size(70, 40), Measure(root));
        Approximately.Equal(new Position(10, 10), Content(root).Position);
    }

    [Fact]
    public void PaddingHorizontalInsetsTheLeftAndRight()
    {
        Element root = Compose(container => container.PaddingHorizontal(10));

        Approximately.Equal(new Size(70, 20), Measure(root));
        Approximately.Equal(new Position(10, 0), Content(root).Position);
    }

    [Fact]
    public void PaddingVerticalInsetsTheTopAndBottom()
    {
        Element root = Compose(container => container.PaddingVertical(10));

        Approximately.Equal(new Size(50, 40), Measure(root));
        Approximately.Equal(new Position(0, 10), Content(root).Position);
    }

    [Fact]
    public void PaddingLeftInsetsOnlyTheLeft()
    {
        Element root = Compose(container => container.PaddingLeft(10));

        Approximately.Equal(new Size(60, 20), Measure(root));
        Approximately.Equal(new Position(10, 0), Content(root).Position);
    }

    [Fact]
    public void PaddingRightInsetsOnlyTheRight()
    {
        Element root = Compose(container => container.PaddingRight(10));

        Approximately.Equal(new Size(60, 20), Measure(root));
        Approximately.Equal(new Position(0, 0), Content(root).Position);
    }

    [Fact]
    public void PaddingTopInsetsOnlyTheTop()
    {
        Element root = Compose(container => container.PaddingTop(10));

        Approximately.Equal(new Size(50, 30), Measure(root));
        Approximately.Equal(new Position(0, 10), Content(root).Position);
    }

    [Fact]
    public void PaddingBottomInsetsOnlyTheBottom()
    {
        Element root = Compose(container => container.PaddingBottom(10));

        Approximately.Equal(new Size(50, 30), Measure(root));
        Approximately.Equal(new Position(0, 0), Content(root).Position);
    }

    // ---- Painting ------------------------------------------------------------------------------------------

    // How far a background or border extends is the painting element's business, not the fluent API's: these
    // assert what was attached — colour, side, thickness, and that the content is still drawn — and never the
    // extent of the painted box.

    [Fact]
    public void BackgroundAcceptsHex()
    {
        Element root = Compose(container => container.Background("#00FF00"));

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
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.BorderLeft(3)), TestInks.Black));

        Approximately.Equal(new Position(0, 0), band.Position);
        Approximately.Equal(3f, band.Size.Width);
        Assert.True(band.Size.Height > band.Size.Width, "A left border is an upright band.");
    }

    [Fact]
    public void BorderRightDrawsOnlyTheRightBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.BorderRight(3)), TestInks.Black));

        Approximately.Equal(0f, band.Position.Y);
        Approximately.Equal(3f, band.Size.Width);
        Assert.True(band.Position.X >= 47f, $"A right border belongs at the far edge, not at x = {band.Position.X}.");
    }

    [Fact]
    public void BorderTopDrawsOnlyTheTopBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.BorderTop(3)), TestInks.Black));

        Approximately.Equal(new Position(0, 0), band.Position);
        Approximately.Equal(3f, band.Size.Height);
        Assert.True(band.Size.Width > band.Size.Height, "A top border is a flat band.");
    }

    [Fact]
    public void BorderBottomDrawsOnlyTheBottomBand()
    {
        RectangleOperation band = Assert.Single(Bands(Compose(container => container.BorderBottom(3)), TestInks.Black));

        Approximately.Equal(0f, band.Position.X);
        Approximately.Equal(3f, band.Size.Height);
        Assert.True(band.Position.Y >= 17f, $"A bottom border belongs at the far edge, not at y = {band.Position.Y}.");
    }

    [Fact]
    public void BorderColorRecoloursTheBorderItFollows()
    {
        Element root = Compose(container => container.Border(2).BorderColor(TestInks.Blue));

        Assert.Equal(4, Bands(root, TestInks.Blue).Count);
        Assert.Empty(Bands(root, TestInks.Black));
    }

    [Fact]
    public void BorderColorAcceptsHex()
    {
        Element root = Compose(container => container.Border(2).BorderColor("#0000FF"));

        Assert.Equal(4, Bands(root, Ink.Rgb(0, 0, 255)).Count);
    }

    [Fact]
    public void BorderColorMustFollowABorder()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Compose(container => container.Padding(2).BorderColor(TestInks.Blue)));

        Assert.Equal("BorderColor must be applied directly after a Border method.", exception.Message);
    }

    [Fact]
    public void CornerRadiusMustFollowABackgroundOrBorder()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Compose(container => container.Padding(2).CornerRadius(4)));

        Assert.Equal("CornerRadius must be applied directly after a Background or Border method.", exception.Message);
    }

    [Fact]
    public void CornerRadiusExplainsWhyAnUnevenBorderCannotBeRounded()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Compose(container => container.BorderLeft(2).CornerRadius(4)));

        Assert.Equal(
            "CornerRadius requires a border of uniform width. Use Border(width) rather than a single-sided " +
            "BorderLeft/Right/Top/Bottom, and give it a width greater than zero.",
            exception.Message);
    }

    [Fact]
    public void AZeroCornerRadiusIsAcceptedOnAnUnevenBorder()
    {
        // Zero asks for square corners, which every border can draw, so there is nothing to refuse.
        Element root = Compose(container => container.BorderLeft(2).CornerRadius(0));

        RecordedPage page = LayoutHarness.Draw(root, Space);

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Black);
    }

    // ---- Sizing --------------------------------------------------------------------------------------------

    [Fact]
    public void WidthPinsTheWidth()
    {
        Approximately.Equal(new Size(80, 20), Measure(Compose(container => container.Width(80))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.Width(80), width: 120), Space).IsWrap);
    }

    [Fact]
    public void MinWidthGrowsNarrowContentButNotWideContent()
    {
        Approximately.Equal(new Size(80, 20), Measure(Compose(container => container.MinWidth(80))));
        Approximately.Equal(new Size(120, 20), Measure(Compose(container => container.MinWidth(80), width: 120)));
    }

    [Fact]
    public void MaxWidthCapsTheSpaceOfferedButDoesNotGrowContent()
    {
        Approximately.Equal(new Size(50, 20), Measure(Compose(container => container.MaxWidth(80))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.MaxWidth(80), width: 120), Space).IsWrap);
    }

    [Fact]
    public void HeightPinsTheHeight()
    {
        Approximately.Equal(new Size(50, 40), Measure(Compose(container => container.Height(40))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.Height(40), height: 60), Space).IsWrap);
    }

    [Fact]
    public void MinHeightGrowsShortContentButNotTallContent()
    {
        Approximately.Equal(new Size(50, 40), Measure(Compose(container => container.MinHeight(40))));
        Approximately.Equal(new Size(50, 60), Measure(Compose(container => container.MinHeight(40), height: 60)));
    }

    [Fact]
    public void MaxHeightCapsTheSpaceOfferedButDoesNotGrowContent()
    {
        Approximately.Equal(new Size(50, 20), Measure(Compose(container => container.MaxHeight(40))));
        Assert.True(LayoutHarness.Measure(Compose(container => container.MaxHeight(40), height: 60), Space).IsWrap);
    }

    [Fact]
    public void ExtendClaimsTheWholeSpace() =>
        Approximately.Equal(new Size(200, 100), Measure(Compose(container => container.Extend())));

    [Fact]
    public void ExtendHorizontalClaimsOnlyTheWidth() =>
        Approximately.Equal(new Size(200, 20), Measure(Compose(container => container.ExtendHorizontal())));

    [Fact]
    public void ExtendVerticalClaimsOnlyTheHeight() =>
        Approximately.Equal(new Size(50, 100), Measure(Compose(container => container.ExtendVertical())));

    [Fact]
    public void AspectRatioDerivesTheHeightFromTheWidthByDefault() =>
        Approximately.Equal(new Size(200, 50), Measure(Compose(container => container.AspectRatio(4))));

    [Fact]
    public void AspectRatioCanDeriveTheWidthFromTheHeight() =>
        Approximately.Equal(
            new Size(50, 100),
            Measure(Compose(container => container.AspectRatio(0.5f, AspectRatioOption.FitHeight))));

    [Fact]
    public void ScaleToFitShrinksNoFurtherThanTheMinimumScale()
    {
        // A 400pt block needs half scale to fit 200pt. The default floor of a quarter allows that; a floor of
        // three quarters does not, so the block is passed through unscaled and cannot be placed.
        Element shrinkable = Compose(container => container.ScaleToFit(), width: 400);
        Element barelyShrinkable = Compose(container => container.ScaleToFit(0.75f), width: 400);

        Assert.True(LayoutHarness.Measure(shrinkable, Space).IsFullRender);
        Assert.True(LayoutHarness.Measure(barelyShrinkable, Space).IsWrap);
    }

    // ---- Flipping ------------------------------------------------------------------------------------------

    // A flip mirrors the block within its own box, so only the corner the drawing starts from moves.

    [Fact]
    public void FlipHorizontalStartsTheContentFromItsRightEdge()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(container => container.FlipHorizontal()));

        Approximately.Equal(new Position(50, 0), content.Position);
        Approximately.Equal(new Position(0, 0), new Position(content.Bounds.Left, content.Bounds.Top));
    }

    [Fact]
    public void FlipVerticalStartsTheContentFromItsBottomEdge()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(container => container.FlipVertical()));

        Approximately.Equal(new Position(0, 20), content.Position);
        Approximately.Equal(new Position(0, 0), new Position(content.Bounds.Left, content.Bounds.Top));
    }

    [Fact]
    public void FlipOverStartsTheContentFromTheOppositeCorner()
    {
        RectangleOperation content = ContentInItsOwnBox(Compose(container => container.FlipOver()));

        Approximately.Equal(new Position(50, 20), content.Position);
        Approximately.Equal(new Position(0, 0), new Position(content.Bounds.Left, content.Bounds.Top));
    }

    // ---- Alignment -----------------------------------------------------------------------------------------

    [Fact]
    public void AlignLeftClaimsTheWidthAndKeepsContentAtTheLeft()
    {
        Element root = Compose(container => container.AlignLeft());

        Approximately.Equal(new Size(200, 20), Measure(root));
        Approximately.Equal(new Position(0, 0), Content(root).Position);
    }

    [Fact]
    public void AlignCenterCentresHorizontally() =>
        Approximately.Equal(new Position(75, 0), Content(Compose(container => container.AlignCenter())).Position);

    [Fact]
    public void AlignRightMovesContentToTheRight() =>
        Approximately.Equal(new Position(150, 0), Content(Compose(container => container.AlignRight())).Position);

    [Fact]
    public void AlignTopClaimsTheHeightAndKeepsContentAtTheTop()
    {
        Element root = Compose(container => container.AlignTop());

        Approximately.Equal(new Size(50, 100), Measure(root));
        Approximately.Equal(new Position(0, 0), Content(root).Position);
    }

    [Fact]
    public void AlignMiddleCentresVertically() =>
        Approximately.Equal(new Position(0, 40), Content(Compose(container => container.AlignMiddle())).Position);

    [Fact]
    public void AlignBottomMovesContentToTheBottom() =>
        Approximately.Equal(new Position(0, 80), Content(Compose(container => container.AlignBottom())).Position);

    [Fact]
    public void AHorizontalAlignmentFoldsIntoAPrecedingVerticalOne()
    {
        Container container = new Container();

        IContainer vertical = container.AlignBottom();
        IContainer both = vertical.AlignCenter();
        both.Element(inner => inner.Child = new FixedElement(50, 20, TestInks.Red));

        Assert.Same(vertical, both);
        Approximately.Equal(new Position(75, 80), Content(container).Position);
    }

    [Fact]
    public void ALaterAlignmentOnTheSameAxisReplacesTheEarlierOne()
    {
        Container container = new Container();

        IContainer first = container.AlignLeft().AlignTop();
        IContainer second = first.AlignRight().AlignBottom();
        second.Element(inner => inner.Child = new FixedElement(50, 20, TestInks.Red));

        Assert.Same(first, second);
        Approximately.Equal(new Position(150, 80), Content(container).Position);
    }

    [Fact]
    public void AnAlignerThatAlreadyHoldsContentIsNotRealigned()
    {
        // Folding into a filled aligner would silently move content composed earlier, so the call is treated as
        // new composition in an occupied slot and refused.
        Container container = new Container();
        IContainer aligned = container.AlignRight();
        aligned.Element(inner => inner.Child = new FixedElement(50, 20, TestInks.Red));

        Assert.Throws<DocumentComposeException>(() => aligned.AlignCenter());
        Approximately.Equal(new Position(150, 0), Content(container).Position);
    }

    // ---- Transforms ----------------------------------------------------------------------------------------

    [Fact]
    public void TranslateXShiftsTheDrawingButNotTheLayout()
    {
        Element root = Compose(container => container.TranslateX(15));

        Approximately.Equal(new Size(50, 20), Measure(root));
        Approximately.Equal(new Position(15, 0), Content(root).Position);
    }

    [Fact]
    public void TranslateYShiftsTheDrawingButNotTheLayout()
    {
        Element root = Compose(container => container.TranslateY(15));

        Approximately.Equal(new Size(50, 20), Measure(root));
        Approximately.Equal(new Position(0, 15), Content(root).Position);
    }

    [Fact]
    public void ScaleResizesBothAxesEqually()
    {
        Element root = Compose(container => container.Scale(0.5f));
        Bounds bounds = Content(root).Bounds;

        Approximately.Equal(new Size(25, 10), Measure(root));
        Approximately.Equal(new Size(25, 10), new Size(bounds.Width, bounds.Height));
    }

    [Fact]
    public void ScaleResizesEachAxisIndependently()
    {
        Element root = Compose(container => container.Scale(0.5f, 2f));
        Bounds bounds = Content(root).Bounds;

        Approximately.Equal(new Size(25, 40), Measure(root));
        Approximately.Equal(new Size(25, 40), new Size(bounds.Width, bounds.Height));
    }

    // Both rotations swap the axes; they differ in which corner the content is drawn from.

    [Fact]
    public void RotateRightTurnsAQuarterClockwise()
    {
        Element root = Compose(container => container.RotateRight());
        RectangleOperation content = ContentInItsOwnBox(root);

        Approximately.Equal(new Size(20, 50), Measure(root));
        Approximately.Equal(new Position(20, 0), content.Position);
        Approximately.Equal(new Size(20, 50), new Size(content.Bounds.Width, content.Bounds.Height));
    }

    [Fact]
    public void RotateLeftTurnsAQuarterAnticlockwise()
    {
        Element root = Compose(container => container.RotateLeft());
        RectangleOperation content = ContentInItsOwnBox(root);

        Approximately.Equal(new Size(20, 50), Measure(root));
        Approximately.Equal(new Position(0, 50), content.Position);
        Approximately.Equal(new Size(20, 50), new Size(content.Bounds.Width, content.Bounds.Height));
    }

    // ---- Flow control --------------------------------------------------------------------------------------

    [Fact]
    public void ShowIfTrueKeepsTheContent()
    {
        Element root = Compose(container => container.ShowIf(true));

        Approximately.Equal(new Size(50, 20), Measure(root));
        Assert.Single(LayoutHarness.Draw(root, Space).Operations);
    }

    [Fact]
    public void ShowIfFalseRemovesTheContent()
    {
        Element root = Compose(container => container.ShowIf(false));

        Approximately.Equal(Size.Zero, Measure(root));
        Assert.Empty(LayoutHarness.Draw(root, Space).Operations);
    }

    [Fact]
    public void ShowOnceDrawsTheContentOnlyTheFirstTime()
    {
        Element root = Compose(container => container.ShowOnce());

        Assert.Single(LayoutHarness.Draw(root, Space).Operations);
        Assert.Empty(LayoutHarness.Draw(root, Space).Operations);
    }

    // ---- Inherited context ---------------------------------------------------------------------------------

    [Fact]
    public void LeftToRightRestoresTheUsualDirectionInsideARightToLeftPassage()
    {
        Element nested = LayoutHarness.Build(container => container.RightToLeft().LeftToRight().Text("Hello"));
        Element outer = LayoutHarness.Build(container => container.RightToLeft().Text("Hello"));

        // Right-to-left text hugs the right edge: 200 less five 6pt characters.
        Approximately.Equal(0f, Assert.Single(LayoutHarness.Draw(nested, Space).Texts).Position.X);
        Approximately.Equal(170f, Assert.Single(LayoutHarness.Draw(outer, Space).Texts).Position.X);
    }

    [Fact]
    public void DefaultTextStyleRefusesAMissingRefinement()
    {
        Container container = new Container();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => container.DefaultTextStyle(null!));

        Assert.Equal("refinement", exception.ParamName);
        Assert.Null(container.Child);
    }

    // ---- Sizing escapes ------------------------------------------------------------------------------------

    [Fact]
    public void UnconstrainedContentOverflowsWhileReportingNoSize()
    {
        Element root = Compose(container => container.Unconstrained(), width: 300);

        Approximately.Equal(Size.Zero, Measure(root));
        Approximately.Equal(new Size(300, 20), Content(root).Size);
    }

    // ---- Rules and placeholders ----------------------------------------------------------------------------

    [Fact]
    public void LineHorizontalDefaultsToAThinBlackRule()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.LineHorizontal()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Black, rule.Color);
        Approximately.Equal(new Size(200, 1), rule.Size);
    }

    [Fact]
    public void LineHorizontalTakesAThicknessAndColour()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.LineHorizontal(3, TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, rule.Color);
        Approximately.Equal(new Size(200, 3), rule.Size);
    }

    [Fact]
    public void LineVerticalDefaultsToAThinBlackRule()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.LineVertical()), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal(TestInks.Black, rule.Color);
        Approximately.Equal(new Size(1, 100), rule.Size);
    }

    [Fact]
    public void LineVerticalTakesAThicknessAndColour()
    {
        RectangleOperation rule = Assert.Single(
            LayoutHarness.Draw(LayoutHarness.Build(container => container.LineVertical(3, TestInks.Red)), Space)
                .Operations.OfType<RectangleOperation>());

        Assert.Equal((Ink)TestInks.Red, rule.Color);
        Approximately.Equal(new Size(3, 100), rule.Size);
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
        RecordedPage page = LayoutHarness.Draw(Compose(container => container.Hyperlink("https://example.com")), Space);

        Assert.Equal("https://example.com", Assert.Single(page.Operations.OfType<ExternalLinkOperation>()).Url);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void SectionNamesADestination()
    {
        RecordedPage page = LayoutHarness.Draw(Compose(container => container.Section("intro")), Space);

        Assert.Equal("intro", Assert.Single(page.Operations.OfType<DestinationOperation>()).Name);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void SectionLinkMakesTheContentJumpToTheSection()
    {
        RecordedPage page = LayoutHarness.Draw(Compose(container => container.SectionLink("intro")), Space);

        Assert.Equal("intro", Assert.Single(page.Operations.OfType<InternalLinkOperation>()).Destination);
        Assert.Single(page.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LinkTargetsNameTheArgumentTheyReject(string? target)
    {
        Container container = new Container();

        Assert.Equal("url", Assert.ThrowsAny<ArgumentException>(() => container.Hyperlink(target!)).ParamName);
        Assert.Equal("name", Assert.ThrowsAny<ArgumentException>(() => container.Section(target!)).ParamName);
        Assert.Equal(
            "sectionName",
            Assert.ThrowsAny<ArgumentException>(() => container.SectionLink(target!)).ParamName);
        Assert.Null(container.Child);
    }
}
