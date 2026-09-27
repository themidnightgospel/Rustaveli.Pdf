using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Services and state available to an element while it is being measured.
/// </summary>
internal sealed class PlanContext(ITypeMeasurer textMeasurer, Pagination page)
{
    public ITypeMeasurer Measurer { get; } = textMeasurer;

    public Pagination Pagination { get; } = page;

    /// <summary>The resolution images generated at their final size are generated at, in pixels per inch.</summary>
    public float Resolution { get; internal set; } = 288;

    /// <summary>
    /// The style text inherits when it specifies none of its own. Set per page run before rendering, which lets
    /// a document establish a typeface once rather than at every span.
    /// </summary>
    public TypeStyle DefaultType { get; internal set; } = TypeStyle.Default;

    /// <summary>
    /// The direction sibling content flows in. Inherited by rows, tables and text that do not state their own,
    /// so a right-to-left document reverses throughout without every element repeating it.
    /// </summary>
    public ReadingDirection ReadingDirection { get; internal set; } = ReadingDirection.LeftToRight;

    /// <summary>
    /// Runs <paramref name="action" /> with a different content direction in force, restoring the previous one
    /// afterwards even if it throws.
    /// </summary>
    internal void WithReadingDirection(ReadingDirection direction, Action action)
    {
        ReadingDirection contentDirection = ReadingDirection;
        ReadingDirection = direction;
        try
        {
            action();
        }
        finally
        {
            ReadingDirection = contentDirection;
        }
    }

    /// <summary>Runs <paramref name="function" /> with a different content direction in force.</summary>
    internal T WithReadingDirection<T>(ReadingDirection direction, Func<T> function)
    {
        ReadingDirection contentDirection = ReadingDirection;
        ReadingDirection = direction;
        try
        {
            return function();
        }
        finally
        {
            ReadingDirection = contentDirection;
        }
    }
}
