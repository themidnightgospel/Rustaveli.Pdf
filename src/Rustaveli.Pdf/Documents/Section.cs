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
public sealed class Section
{
    public Extent Size { get; set; } = PaperSizes.A4;

    public Sides Margin { get; set; } = Sides.All(0);

    public Ink BackgroundColor { get; set; } = Ink.White;

    public ReadingDirection Direction { get; set; } = ReadingDirection.LeftToRight;

    /// <summary>Style inherited by any text that does not override it.</summary>
    public TypeStyle DefaultTextStyle { get; set; } = TypeStyle.Default;

    /// <summary>
    /// When set, the page grows vertically to fit its content instead of using a fixed height, capped at the
    /// PDF maximum of 14400 points.
    /// </summary>
    public bool IsContinuous { get; set; }

    internal Frame HeaderSlot { get; } = new();

    internal Frame ContentSlot { get; } = new();

    internal Frame FooterSlot { get; } = new();

    internal Frame BackgroundSlot { get; } = new();

    internal Frame ForegroundSlot { get; } = new();

    public IFrame Header() => HeaderSlot;

    public IFrame Content() => ContentSlot;

    public IFrame Footer() => FooterSlot;

    public IFrame Background() => BackgroundSlot;

    public IFrame Foreground() => ForegroundSlot;

    internal IEnumerable<Block> Slots()
    {
        yield return HeaderSlot;
        yield return ContentSlot;
        yield return FooterSlot;
        yield return BackgroundSlot;
        yield return ForegroundSlot;
    }
}
