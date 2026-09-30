namespace Rustaveli.Pdf.Blocks;

/// <summary>How the width of one item in a row of columns is decided.</summary>
internal enum ColumnSizing
{
    /// <summary>A share of the width the other items leave, in proportion to the item's weight.</summary>
    Share,

    /// <summary>A width in points.</summary>
    Fixed,

    /// <summary>As wide as the item's content needs.</summary>
    Natural,
}
