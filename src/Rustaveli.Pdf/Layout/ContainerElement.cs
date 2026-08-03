using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// An element wrapping exactly one child. Measuring and drawing delegate straight through, so derived types
/// only override the behaviour they actually change.
/// </summary>
public abstract class ContainerElement : Element, IContainer
{
    public Element? Child { get; set; }

    public override IEnumerable<Element?> GetChildren()
    {
        yield return Child;
    }

    // An absent child renders completely and occupies nothing, which is distinct from Empty: Empty means a
    // stateful child has exhausted its content and must not be given space again on continuation pages.
    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        Child?.Measure(availableSpace, context) ?? SpacePlan.FullRender(Size.Zero);

    public override void Draw(Size availableSpace, DrawContext context) =>
        Child?.Draw(availableSpace, context);
}
