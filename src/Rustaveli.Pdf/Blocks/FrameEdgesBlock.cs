using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws its content, then the edges of the room it was given as a dashed outline, with a label in the top corner if
/// it has one: a frame's extent made visible, as a layout application shows frame edges.
/// </summary>
internal sealed class FrameEdgesBlock : EnclosingBlock
{
    private const float OutlineWeight = 0.5f;

    private static readonly float[] Dashes = [3, 2];

    /// <summary>How far the label sits in from the edges of its tab, across and down.</summary>
    private static readonly Offset LabelMargin = new Offset(2, 1);

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

    internal override int ChildCount => 2;

    internal override Block? ChildAt(int index) => index switch
    {
        0 => Child,
        1 => _label,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        base.RenderCore(availableSpace, context);

        Offset topRight = new Offset(availableSpace.Width, 0);
        Offset bottomLeft = new Offset(0, availableSpace.Height);
        Offset bottomRight = new Offset(availableSpace.Width, availableSpace.Height);

        context.Surface.DrawDashedLine(Offset.Zero, topRight, OutlineWeight, Ink, Dashes);
        context.Surface.DrawDashedLine(topRight, bottomRight, OutlineWeight, Ink, Dashes);
        context.Surface.DrawDashedLine(bottomRight, bottomLeft, OutlineWeight, Ink, Dashes);
        context.Surface.DrawDashedLine(bottomLeft, Offset.Zero, OutlineWeight, Ink, Dashes);

        if (_label is null)
            return;

        // The label sits on a tab of the outline's ink in the top corner, as small as its words.
        Fit fit = _label.Plan(Extent.Max, context.Planning);

        if (!fit.PlacesContent)
            return;

        Extent tab = new Extent(fit.Size.Width + (2 * LabelMargin.X), fit.Size.Height + (2 * LabelMargin.Y));
        context.Surface.FillRectangle(Offset.Zero, tab, Ink);
        context.Surface.MoveOrigin(LabelMargin);
        _label.Render(fit.Size, context);
        context.Surface.MoveOrigin(LabelMargin.Reverse());
        _label.ResetState();
    }
}
