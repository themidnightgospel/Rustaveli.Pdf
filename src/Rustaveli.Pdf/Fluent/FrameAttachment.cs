using Rustaveli.Pdf.Exceptions;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

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

        if (parent.Child is not null)
        {
            throw new CompositionException(
                $"This frame already holds {parent.Child.GetType().Name} and cannot also hold {element.GetType().Name}. " +
                "A frame holds a single piece of content; use Stack, Columns or Layered to place more than one.");
        }

        parent.Child = element;

        return element;
    }
}
