namespace Rustaveli.Pdf.UnitTests;

public class LayoutContextTests
{
    [Fact]
    public void ExposesTheServicesItWasCreatedWith()
    {
        ITextMeasurer measurer = new FakeTextMeasurer();
        PageContext page = new PageContext();

        LayoutContext context = new LayoutContext(measurer, page);

        Assert.Same(measurer, context.TextMeasurer);
        Assert.Same(page, context.Page);
    }

    [Fact]
    public void StartsWithTheDefaultStyleFlowingLeftToRight()
    {
        LayoutContext context = LayoutHarness.Context();

        Assert.Same(TextStyle.Default, context.DefaultTextStyle);
        Assert.Equal(ContentDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AnActionRunsWithTheRequestedDirectionInForce()
    {
        LayoutContext context = LayoutHarness.Context();
        ContentDirection observed = ContentDirection.LeftToRight;

        context.WithDirection(ContentDirection.RightToLeft, () => { observed = context.ContentDirection; });

        Assert.Equal(ContentDirection.RightToLeft, observed);
        Assert.Equal(ContentDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AnActionRestoresThePreviousDirectionRatherThanTheDefault()
    {
        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        context.WithDirection(ContentDirection.LeftToRight, () => { });

        Assert.Equal(ContentDirection.RightToLeft, context.ContentDirection);
    }

    [Fact]
    public void AnActionThatThrowsStillRestoresTheDirection()
    {
        LayoutContext context = LayoutHarness.Context();
        InvalidOperationException failure = new InvalidOperationException("boom");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            context.WithDirection(ContentDirection.RightToLeft, () => { throw failure; }));

        Assert.Same(failure, thrown);
        Assert.Equal(ContentDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AFunctionRunsWithTheRequestedDirectionAndReturnsItsResult()
    {
        LayoutContext context = LayoutHarness.Context();

        ContentDirection observed = context.WithDirection(ContentDirection.RightToLeft, () => context.ContentDirection);

        Assert.Equal(ContentDirection.RightToLeft, observed);
        Assert.Equal(ContentDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AFunctionRestoresThePreviousDirectionRatherThanTheDefault()
    {
        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        int result = context.WithDirection(ContentDirection.LeftToRight, () => 42);

        Assert.Equal(42, result);
        Assert.Equal(ContentDirection.RightToLeft, context.ContentDirection);
    }

    [Fact]
    public void AFunctionThatThrowsStillRestoresTheDirection()
    {
        LayoutContext context = LayoutHarness.Context();
        InvalidOperationException failure = new InvalidOperationException("boom");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            context.WithDirection<int>(ContentDirection.RightToLeft, () => throw failure));

        Assert.Same(failure, thrown);
        Assert.Equal(ContentDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void NestedOverridesUnwindOneLevelAtATime()
    {
        LayoutContext context = LayoutHarness.Context();
        List<ContentDirection> seen = [];

        context.WithDirection(ContentDirection.RightToLeft, () =>
        {
            context.WithDirection(ContentDirection.LeftToRight, () => { seen.Add(context.ContentDirection); });
            seen.Add(context.ContentDirection);
        });
        seen.Add(context.ContentDirection);

        Assert.Equal(
            new[] { ContentDirection.LeftToRight, ContentDirection.RightToLeft, ContentDirection.LeftToRight },
            seen);
    }
}
