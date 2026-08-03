using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// How a row allocates horizontal space to one of its items.
/// </summary>
public enum RowItemSizing
{
    /// <summary>Takes exactly as much width as the content needs.</summary>
    Auto,

    /// <summary>Takes a fixed width in points.</summary>
    Constant,

    /// <summary>Shares the leftover width with other relative items, in proportion to its weight.</summary>
    Relative
}
