using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws its content, then the edges of the room it was given as a dashed outline, with a label in the top corner if
/// it has one: a frame's extent made visible, as a layout application shows frame edges.
/// </summary>
internal sealed class FrameEdgesBlock : EnclosingBlock
{
    private static readonly float[] Dashes = [3, 2];

    private readonly TextBlock? _label;

    public FrameEdgesBlock(string? label, Ink ink)
    {
        Ink = ink;

        if (label is null)
            return;

        _label = new TextBlock { DefaultTypeRefinement = style => style.WithPointSize(6).WithInk(Ink.White) };
        _label.Runs.Add(new TextRun { Text = label });
    }

    public Ink Ink { get; }

    public string? Label => _label?.Runs[0].Text;

    public override IEnumerable<Block?> GetChildren()
    {
        yield return Child;
        yield return _label;
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        base.RenderCore(availableSpace, context);

        Offset topRight = new Offset(availableSpace.Width, 0);
        Offset bottomLeft = new Offset(0, availableSpace.Height);
        Offset bottomRight = new Offset(availableSpace.Width, availableSpace.Height);

        context.Surface.DrawDashedLine(Offset.Zero, topRight, 0.5f, Ink, Dashes);
        context.Surface.DrawDashedLine(topRight, bottomRight, 0.5f, Ink, Dashes);
        context.Surface.DrawDashedLine(bottomRight, bottomLeft, 0.5f, Ink, Dashes);
        context.Surface.DrawDashedLine(bottomLeft, Offset.Zero, 0.5f, Ink, Dashes);

        if (_label is null)
            return;

        // The label sits on a tab of the outline's ink in the top corner, as small as its words.
        Fit fit = _label.Plan(Extent.Max, context.Planning);

        if (fit.IsDeferred || fit.IsNothing)
            return;

        Extent tab = new Extent(fit.Size.Width + 4, fit.Size.Height + 2);
        context.Surface.DrawRectangle(Offset.Zero, tab, Ink);
        context.Surface.Translate(new Offset(2, 1));
        _label.Render(fit.Size, context);
        context.Surface.Translate(new Offset(-2, -1));
        _label.ResetState();
    }
}
