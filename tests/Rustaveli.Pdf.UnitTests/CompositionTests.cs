namespace Rustaveli.Pdf.UnitTests;

public class CompositionTests
{
    [Fact]
    public void RefusesToReplaceContentAlreadyInTheFrame()
    {
        Frame frame = new Frame();
        frame.Inset(5);

        CompositionException exception = Assert.Throws<CompositionException>(() =>
            frame.Fill(TestInks.Red));

        // Both types are named so the message points at the two pieces of composition that collided.
        Assert.Contains("This frame already holds InsetBlock and cannot also hold FillBlock", exception.Message);
        Assert.Contains("use Stack, Columns or Layered", exception.Message);
        Assert.IsType<InsetBlock>(frame.Slot().Child);
    }

    [Fact]
    public void RefusesAMissingFrame()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            FrameModifiers.Inset(null!, 5));

        Assert.Equal("parent", exception.ParamName);
    }

    [Fact]
    public void HandsBackTheAttachedBlockAsTheNextFrame()
    {
        Frame frame = new Frame();

        IFrame next = frame.Inset(5);

        Assert.Same(frame.Slot().Child, next);
    }
}
