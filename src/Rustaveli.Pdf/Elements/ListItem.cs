using System.Text;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A marked item within a list.
/// </summary>
public sealed class ListItem : ContainerElement
{
    /// <summary>The marker text resolved for this item's position.</summary>
    public string Marker { get; set; } = string.Empty;
}
