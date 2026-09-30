namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Shared plumbing for placing blocks into frames.
/// </summary>
internal static class FrameAttachment
{
    /// <summary>
    /// Places <paramref name="block"/> into the frame <paramref name="parent"/> and returns it, so the next call can
    /// place content into it in turn.
    /// </summary>
    /// <remarks>
    /// A frame holds at most one block. Placing another over it would discard a whole subtree without a word — a
    /// snippet that composes into the same frame twice would simply lose what it placed first — so the second
    /// attempt is refused instead.
    /// </remarks>
    public static T Attach<T>(IFrame parent, T block) where T : Block
    {
        ArgumentNullException.ThrowIfNull(parent);
        IFrameSlot slot = Slot(parent);

        if (slot.Child is not null)
        {
            throw new CompositionException(
                $"This frame already holds {slot.Child.GetType().Name} and cannot also hold {block.GetType().Name}. " +
                "A frame holds a single piece of content; use Stack, Columns or Layered to place more than one.");
        }

        slot.Child = block;

        return block;
    }

    /// <summary>The slot behind a frame the library made.</summary>
    /// <remarks>
    /// <see cref="IFrame"/> is public so that it can be passed around, but only the library's own frames hold
    /// content; one implemented elsewhere has nowhere to put it.
    /// </remarks>
    public static IFrameSlot Slot(IFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        return frame as IFrameSlot ?? throw new CompositionException(
            $"{frame.GetType().Name} is not a frame this library made. Frames come from sections, composers and " +
            "modifiers; implement ISnippet to package composition of your own.");
    }
}
