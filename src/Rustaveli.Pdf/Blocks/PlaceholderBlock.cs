using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A filled block standing in for content that does not exist yet.
/// </summary>
internal sealed class PlaceholderBlock : Block
{
    private readonly TextBlock? _label;

    public PlaceholderBlock(string? label = null)
    {
        if (label is null)
            return;

        _label = new TextBlock
        {
            Alignment = LineAlignment.Center,
            DefaultTypeRefinement = style => style.WithInk(Ink.Rgb(0x75, 0x75, 0x75)),
        };

        _label.Runs.Add(new TextRun { Text = label });
    }

    public Ink Ink { get; set; } = Ink.Rgb(0xEE, 0xEE, 0xEE);

    /// <summary>The words shown in the middle of the box, saying what will go there, if any.</summary>
    public string? Label => _label?.Runs[0].Text;

    public override IEnumerable<Block?> GetChildren()
    {
        yield return _label;
    }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Fit.Complete(availableSpace);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        context.Surface.DrawRectangle(Offset.Zero, availableSpace, Ink);

        if (_label is null)
            return;

        // The label is centred as far as it fits; a box too small for a line of it shows the box alone.
        Fit fit = _label.Plan(availableSpace, context.Planning);

        if (fit.IsDeferred || fit.IsNothing)
            return;

        Offset offset = new Offset(0, (availableSpace.Height - fit.Size.Height) / 2);
        context.Surface.Translate(offset);
        _label.Render(new Extent(availableSpace.Width, fit.Size.Height), context);
        context.Surface.Translate(offset.Reverse());
        _label.ResetState();
    }
}
