using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Documents;

/// <summary>
/// Configuration and content slots for one run of pages sharing a size and margin.
/// </summary>
/// <remarks>
/// Header and footer are drawn complete on every page and never paginate. Only <see cref="Content"/> flows,
/// which is what determines how many pages the run produces. Background and foreground ignore margins and cover
/// the whole sheet, making them the natural home for watermarks.
/// </remarks>
public sealed class PageDescriptor
{
    public Size Size { get; set; } = PageSizes.A4;

    public Edges Margin { get; set; } = Edges.All(0);

    public Color BackgroundColor { get; set; } = Colors.White;

    public ContentDirection Direction { get; set; } = ContentDirection.LeftToRight;

    /// <summary>Style inherited by any text that does not override it.</summary>
    public TextStyle DefaultTextStyle { get; set; } = TextStyle.Default;

    /// <summary>
    /// When set, the page grows vertically to fit its content instead of using a fixed height, capped at the
    /// PDF maximum of 14400 points.
    /// </summary>
    public bool IsContinuous { get; set; }

    internal Container HeaderSlot { get; } = new();

    internal Container ContentSlot { get; } = new();

    internal Container FooterSlot { get; } = new();

    internal Container BackgroundSlot { get; } = new();

    internal Container ForegroundSlot { get; } = new();

    public IContainer Header() => HeaderSlot;

    public IContainer Content() => ContentSlot;

    public IContainer Footer() => FooterSlot;

    public IContainer Background() => BackgroundSlot;

    public IContainer Foreground() => ForegroundSlot;

    internal IEnumerable<Element> Slots()
    {
        yield return HeaderSlot;
        yield return ContentSlot;
        yield return FooterSlot;
        yield return BackgroundSlot;
        yield return ForegroundSlot;
    }
}
