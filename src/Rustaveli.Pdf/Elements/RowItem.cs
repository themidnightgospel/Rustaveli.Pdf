using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A single column of a <see cref="RowElement"/>.
/// </summary>
public sealed class RowItem : ContainerElement
{
    public RowItemSizing Sizing { get; set; } = RowItemSizing.Relative;

    /// <summary>A width in points for constant items, or a weight for relative items.</summary>
    public float Value { get; set; } = 1f;
}
