using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Services and state available to an element while it is being measured.
/// </summary>
public sealed class LayoutContext(ITextMeasurer textMeasurer, PageContext page)
{
    public ITextMeasurer TextMeasurer { get; } = textMeasurer;

    public PageContext Page { get; } = page;

    /// <summary>
    /// The style text inherits when it specifies none of its own. Set per page run before rendering, which lets
    /// a document establish a typeface once rather than at every span.
    /// </summary>
    public TextStyle DefaultTextStyle { get; internal set; } = TextStyle.Default;

    /// <summary>
    /// The direction sibling content flows in. Inherited by rows, tables and text that do not state their own,
    /// so a right-to-left document reverses throughout without every element repeating it.
    /// </summary>
    public ContentDirection ContentDirection { get; internal set; } = ContentDirection.LeftToRight;

    /// <summary>
    /// Runs <paramref name="action" /> with a different content direction in force, restoring the previous one
    /// afterwards even if it throws.
    /// </summary>
    internal void WithDirection(ContentDirection direction, Action action)
    {
        ContentDirection contentDirection = ContentDirection;
        ContentDirection = direction;
        try
        {
            action();
        }
        finally
        {
            ContentDirection = contentDirection;
        }
    }

    /// <summary>Runs <paramref name="function" /> with a different content direction in force.</summary>
    internal T WithDirection<T>(ContentDirection direction, Func<T> function)
    {
        ContentDirection contentDirection = ContentDirection;
        ContentDirection = direction;
        try
        {
            return function();
        }
        finally
        {
            ContentDirection = contentDirection;
        }
    }
}
