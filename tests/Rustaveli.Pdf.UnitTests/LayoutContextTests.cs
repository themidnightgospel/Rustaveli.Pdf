namespace Rustaveli.Pdf.UnitTests;

public class LayoutContextTests
{
    [Fact]
    public void ExposesTheServicesItWasCreatedWith()
    {
        ITypeMeasurer measurer = new FakeTextMeasurer();
        Pagination page = new Pagination();

        PlanContext context = new PlanContext(measurer, page);

        Assert.Same(measurer, context.TextMeasurer);
        Assert.Same(page, context.Page);
    }

    [Fact]
    public void StartsWithTheDefaultStyleFlowingLeftToRight()
    {
        PlanContext context = LayoutHarness.Context();

        Assert.Same(TypeStyle.Default, context.DefaultTextStyle);
        Assert.Equal(ReadingDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AnActionRunsWithTheRequestedDirectionInForce()
    {
        PlanContext context = LayoutHarness.Context();
        ReadingDirection observed = ReadingDirection.LeftToRight;

        context.WithDirection(ReadingDirection.RightToLeft, () => { observed = context.ContentDirection; });

        Assert.Equal(ReadingDirection.RightToLeft, observed);
        Assert.Equal(ReadingDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AnActionRestoresThePreviousDirectionRatherThanTheDefault()
    {
        PlanContext context = LayoutHarness.Context();
        context.ContentDirection = ReadingDirection.RightToLeft;

        context.WithDirection(ReadingDirection.LeftToRight, () => { });

        Assert.Equal(ReadingDirection.RightToLeft, context.ContentDirection);
    }

    [Fact]
    public void AnActionThatThrowsStillRestoresTheDirection()
    {
        PlanContext context = LayoutHarness.Context();
        InvalidOperationException failure = new InvalidOperationException("boom");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            context.WithDirection(ReadingDirection.RightToLeft, () => { throw failure; }));

        Assert.Same(failure, thrown);
        Assert.Equal(ReadingDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AFunctionRunsWithTheRequestedDirectionAndReturnsItsResult()
    {
        PlanContext context = LayoutHarness.Context();

        ReadingDirection observed = context.WithDirection(ReadingDirection.RightToLeft, () => context.ContentDirection);

        Assert.Equal(ReadingDirection.RightToLeft, observed);
        Assert.Equal(ReadingDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void AFunctionRestoresThePreviousDirectionRatherThanTheDefault()
    {
        PlanContext context = LayoutHarness.Context();
        context.ContentDirection = ReadingDirection.RightToLeft;

        int result = context.WithDirection(ReadingDirection.LeftToRight, () => 42);

        Assert.Equal(42, result);
        Assert.Equal(ReadingDirection.RightToLeft, context.ContentDirection);
    }

    [Fact]
    public void AFunctionThatThrowsStillRestoresTheDirection()
    {
        PlanContext context = LayoutHarness.Context();
        InvalidOperationException failure = new InvalidOperationException("boom");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            context.WithDirection<int>(ReadingDirection.RightToLeft, () => throw failure));

        Assert.Same(failure, thrown);
        Assert.Equal(ReadingDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void NestedOverridesUnwindOneLevelAtATime()
    {
        PlanContext context = LayoutHarness.Context();
        List<ReadingDirection> seen = [];

        context.WithDirection(ReadingDirection.RightToLeft, () =>
        {
            context.WithDirection(ReadingDirection.LeftToRight, () => { seen.Add(context.ContentDirection); });
            seen.Add(context.ContentDirection);
        });
        seen.Add(context.ContentDirection);

        Assert.Equal(
            new[] { ReadingDirection.LeftToRight, ReadingDirection.RightToLeft, ReadingDirection.LeftToRight },
            seen);
    }
}
