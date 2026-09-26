
namespace Rustaveli.Pdf.UnitTests;

public class CompositionTests
{
    [Fact]
    public void RefusesToReplaceContentAlreadyInTheContainer()
    {
        Frame container = new Frame();
        container.Inset(5);

        CompositionException exception = Assert.Throws<CompositionException>(() =>
            container.Fill(TestInks.Red));

        // Both types are named so the message points at the two pieces of composition that collided.
        Assert.Contains("This frame already holds InsetBlock and cannot also hold FillBlock", exception.Message);
        Assert.Contains("use Stack, Columns or Layered", exception.Message);
        Assert.IsType<InsetBlock>(container.Slot().Child);
    }

    [Fact]
    public void RefusesAMissingContainer()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            FrameModifiers.Inset(null!, 5));

        Assert.Equal("parent", exception.ParamName);
    }

    [Fact]
    public void HandsBackTheAttachedElementAsTheNextSlot()
    {
        Frame container = new Frame();

        IFrame next = container.Inset(5);

        Assert.Same(container.Slot().Child, next);
    }
}
