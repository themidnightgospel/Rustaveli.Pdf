using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds columns a story flows through as a newspaper's does: down one, on into the next, and on to the next page.
/// </summary>
public sealed class FlowColumnsComposer
{
    private readonly FlowColumnsBlock _block;

    internal FlowColumnsComposer(FlowColumnsBlock block) => _block = block;

    /// <summary>Sets how many columns the story flows through; two unless set.</summary>
    public void Columns(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A story flows through at least one column.");

        _block.Count = count;
    }

    /// <summary>Sets the gap between neighbouring columns.</summary>
    public void Gutter(float value) => _block.Gutter = value;

    /// <summary>Ends the columns of the story's last page level, rather than filling each before the next.</summary>
    public void Balanced() => _block.Balanced = true;

    /// <summary>The frame whose content flows through the columns.</summary>
    public IFrame Story()
    {
        Frame story = new Frame();
        _block.Story = story;
        return story;
    }

    /// <summary>A frame drawn in each gutter between columns in use, as tall as the columns: for a rule, say.</summary>
    public IFrame Between()
    {
        Frame between = new Frame();
        _block.Between = between;
        return between;
    }
}
