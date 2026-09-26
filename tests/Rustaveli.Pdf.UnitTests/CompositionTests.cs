using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

public class CompositionTests
{
    [Fact]
    public void RefusesToReplaceContentAlreadyInTheContainer()
    {
        Container container = new Container();
        container.Padding(5);

        DocumentComposeException exception = Assert.Throws<DocumentComposeException>(() =>
            container.Background(Colors.Red));

        // Both types are named so the message points at the two pieces of composition that collided.
        Assert.Contains("already holds PaddingElement and cannot also hold BackgroundElement", exception.Message);
        Assert.Contains("use Column, Row or Layers", exception.Message);
        Assert.IsType<PaddingElement>(container.Child);
    }

    [Fact]
    public void RefusesAMissingContainer()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            LayoutExtensions.Padding(null!, 5));

        Assert.Equal("parent", exception.ParamName);
    }

    [Fact]
    public void HandsBackTheAttachedElementAsTheNextSlot()
    {
        Container container = new Container();

        IContainer next = container.Padding(5);

        Assert.Same(container.Child, next);
    }
}
