using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// An element wrapping exactly one child. Measuring and drawing delegate straight through, so derived types
/// only override the behaviour they actually change.
/// </summary>
public abstract class EnclosingBlock : Block, IFrame
{
    public Block? Child { get; set; }

    public override IEnumerable<Block?> GetChildren()
    {
        yield return Child;
    }

    // An absent child renders completely and occupies nothing, which is distinct from Empty: Empty means a
    // stateful child has exhausted its content and must not be given space again on continuation pages.
    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Child?.Measure(availableSpace, context) ?? Fit.FullRender(Extent.Zero);

    public override void Draw(Extent availableSpace, RenderContext context) =>
        Child?.Draw(availableSpace, context);
}
