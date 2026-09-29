using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds a flow: items set side by side as words are set in a line, wrapping on to a new line wherever the next
/// would not fit.
/// </summary>
public sealed class FlowComposer
{
    private readonly FlowBlock _block;

    internal FlowComposer(FlowBlock block) => _block = block;

    /// <summary>Sets the gap between neighbouring items in a line.</summary>
    public void Gutter(float value) => _block.Gutter = Numbers.NotNegative(value, nameof(value));

    /// <summary>Sets the gap between one line and the next.</summary>
    public void SpaceBetweenLines(float value) => _block.SpaceBetweenLines = Numbers.NotNegative(value, nameof(value));

    /// <summary>Sets each line against the start, the default.</summary>
    public void FlushLeft() => _block.Placement = FlowPlacement.Left;

    /// <summary>Centres each line.</summary>
    public void Centered() => _block.Placement = FlowPlacement.Center;

    /// <summary>Sets each line against the end.</summary>
    public void FlushRight() => _block.Placement = FlowPlacement.Right;

    /// <summary>Spreads each line but the last across the whole width, widening the gaps between items.</summary>
    public void Justified() => _block.Placement = FlowPlacement.Justify;

    /// <summary>Spreads each line across the whole width, sharing the extra room out around every item.</summary>
    public void SpacedAround() => _block.Placement = FlowPlacement.SpaceAround;

    /// <summary>Sets items shorter than their line against its top, the default.</summary>
    public void FlushTop() => _block.LineAlignment = VerticalPlacement.Top;

    /// <summary>Centres items shorter than their line within it.</summary>
    public void Middle() => _block.LineAlignment = VerticalPlacement.Middle;

    /// <summary>Sets items shorter than their line against its bottom.</summary>
    public void FlushBottom() => _block.LineAlignment = VerticalPlacement.Bottom;

    /// <summary>Adds an item at the end of the flow and returns its frame.</summary>
    public IFrame Add()
    {
        Frame item = new Frame();
        _block.Items.Add(item);
        return item;
    }
}
