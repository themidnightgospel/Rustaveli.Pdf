namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Shared plumbing for attaching elements to containers.
/// </summary>
internal static class FrameAttachment
{
    /// <summary>
    /// Places <paramref name="element"/> into <paramref name="parent"/> and returns it as the next container.
    /// </summary>
    /// <remarks>
    /// A container holds exactly one child. Assigning over an existing one would discard an entire subtree with
    /// no diagnostic — a component that composes into the same slot twice would simply lose its first
    /// contribution — so the second attempt is refused instead.
    /// </remarks>
    public static T Attach<T>(IFrame parent, T element) where T : Block
    {
        ArgumentNullException.ThrowIfNull(parent);
        IFrameSlot slot = Slot(parent);

        if (slot.Child is not null)
        {
            throw new CompositionException(
                $"This frame already holds {slot.Child.GetType().Name} and cannot also hold {element.GetType().Name}. " +
                "A frame holds a single piece of content; use Stack, Columns or Layered to place more than one.");
        }

        slot.Child = element;

        return element;
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
