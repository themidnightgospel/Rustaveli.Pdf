namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// How a row allocates horizontal space to one of its items.
/// </summary>
internal enum ColumnSizing
{
    /// <summary>Takes exactly as much width as the content needs.</summary>
    Natural,

    /// <summary>Takes a fixed width in points.</summary>
    Fixed,

    /// <summary>Shares the leftover width with other relative items, in proportion to its weight.</summary>
    Share
}
