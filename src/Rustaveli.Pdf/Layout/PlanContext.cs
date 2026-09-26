using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Services and state available to an element while it is being measured.
/// </summary>
public sealed class PlanContext(ITypeMeasurer textMeasurer, Pagination page)
{
    public ITypeMeasurer TextMeasurer { get; } = textMeasurer;

    public Pagination Page { get; } = page;

    /// <summary>
    /// The style text inherits when it specifies none of its own. Set per page run before rendering, which lets
    /// a document establish a typeface once rather than at every span.
    /// </summary>
    public TypeStyle DefaultTextStyle { get; internal set; } = TypeStyle.Default;

    /// <summary>
    /// The direction sibling content flows in. Inherited by rows, tables and text that do not state their own,
    /// so a right-to-left document reverses throughout without every element repeating it.
    /// </summary>
    public ReadingDirection ContentDirection { get; internal set; } = ReadingDirection.LeftToRight;

    /// <summary>
    /// Runs <paramref name="action" /> with a different content direction in force, restoring the previous one
    /// afterwards even if it throws.
    /// </summary>
    internal void WithDirection(ReadingDirection direction, Action action)
    {
        ReadingDirection contentDirection = ContentDirection;
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
    internal T WithDirection<T>(ReadingDirection direction, Func<T> function)
    {
        ReadingDirection contentDirection = ContentDirection;
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
