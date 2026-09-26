using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A marked item within a list.
/// </summary>
internal sealed class ListEntry : EnclosingBlock
{
    /// <summary>The marker text resolved for this item's position.</summary>
    public string Marker { get; set; } = string.Empty;
}
