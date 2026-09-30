using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds the contents of a vertical stack.
/// </summary>
public sealed class StackComposer
{
    private readonly StackBlock _block;

    internal StackComposer(StackBlock block) => _block = block;

    /// <summary>Sets the gap inserted between consecutive items.</summary>
    public void SpaceBetween(float value)
    {
        _block.SpaceBetween = Numbers.NotNegative(value, nameof(value));
    }

    /// <summary>Adds an item to the bottom of the stack and returns the frame its content goes into.</summary>
    public IFrame Add()
    {
        Frame item = new Frame();
        _block.Items.Add(item);
        return item;
    }
}
