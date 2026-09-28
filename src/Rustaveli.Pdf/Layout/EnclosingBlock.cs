namespace Rustaveli.Pdf.Layout;

/// <summary>
/// An element wrapping exactly one child. Measuring and drawing delegate straight through, so derived types
/// only override the behaviour they actually change.
/// </summary>
internal abstract class EnclosingBlock : Block, IFrameSlot
{
    public Block? Child { get; set; }

    // A wrapper around repeated content, such as a fill behind it, repeats with it.
    internal override bool Repeats => Child?.Repeats ?? false;

    public override IEnumerable<Block?> GetChildren()
    {
        yield return Child;
    }

    // An absent child renders completely and occupies nothing, which is distinct from Empty: Empty means a
    // stateful child has exhausted its content and must not be given space again on continuation pages.
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Child?.Plan(availableSpace, context) ?? Fit.Complete(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context) =>
        Child?.Render(availableSpace, context);
}
