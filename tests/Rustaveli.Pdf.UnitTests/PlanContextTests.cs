namespace Rustaveli.Pdf.UnitTests;

public class PlanContextTests
{
    [Fact]
    public void ExposesTheServicesItWasCreatedWith()
    {
        ITypeMeasurer measurer = new FakeTypeMeasurer();
        Pagination page = new Pagination();

        PlanContext context = new PlanContext(measurer, page);

        Assert.Same(measurer, context.Measurer);
        Assert.Same(page, context.Pagination);
    }

    [Fact]
    public void StartsWithTheDefaultStyleFlowingLeftToRight()
    {
        PlanContext context = LayoutHarness.Context();

        Assert.Same(TypeStyle.Default, context.DefaultType);
        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Fact]
    public void AnActionRunsWithTheRequestedDirectionInForce()
    {
        PlanContext context = LayoutHarness.Context();
        ReadingDirection observed = ReadingDirection.LeftToRight;

        context.WithReadingDirection(ReadingDirection.RightToLeft, () => { observed = context.ReadingDirection; });

        Assert.Equal(ReadingDirection.RightToLeft, observed);
        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Fact]
    public void AnActionRestoresThePreviousDirectionRatherThanTheDefault()
    {
        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        context.WithReadingDirection(ReadingDirection.LeftToRight, () => { });

        Assert.Equal(ReadingDirection.RightToLeft, context.ReadingDirection);
    }

    [Fact]
    public void AnActionThatThrowsStillRestoresTheDirection()
    {
        PlanContext context = LayoutHarness.Context();
        InvalidOperationException failure = new InvalidOperationException("boom");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            context.WithReadingDirection(ReadingDirection.RightToLeft, () => { throw failure; }));

        Assert.Same(failure, thrown);
        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Fact]
    public void AFunctionRunsWithTheRequestedDirectionAndReturnsItsResult()
    {
        PlanContext context = LayoutHarness.Context();

        ReadingDirection observed = context.WithReadingDirection(ReadingDirection.RightToLeft, () => context.ReadingDirection);

        Assert.Equal(ReadingDirection.RightToLeft, observed);
        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Fact]
    public void AFunctionRestoresThePreviousDirectionRatherThanTheDefault()
    {
        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        int result = context.WithReadingDirection(ReadingDirection.LeftToRight, () => 42);

        Assert.Equal(42, result);
        Assert.Equal(ReadingDirection.RightToLeft, context.ReadingDirection);
    }

    [Fact]
    public void AFunctionThatThrowsStillRestoresTheDirection()
    {
        PlanContext context = LayoutHarness.Context();
        InvalidOperationException failure = new InvalidOperationException("boom");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            context.WithReadingDirection<int>(ReadingDirection.RightToLeft, () => throw failure));

        Assert.Same(failure, thrown);
        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Fact]
    public void NestedOverridesUnwindOneLevelAtATime()
    {
        PlanContext context = LayoutHarness.Context();
        List<ReadingDirection> seen = [];

        context.WithReadingDirection(ReadingDirection.RightToLeft, () =>
        {
            context.WithReadingDirection(ReadingDirection.LeftToRight, () => { seen.Add(context.ReadingDirection); });
            seen.Add(context.ReadingDirection);
        });
        seen.Add(context.ReadingDirection);

        Assert.Equal(
            new[] { ReadingDirection.LeftToRight, ReadingDirection.RightToLeft, ReadingDirection.LeftToRight },
            seen);
    }
}
