using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Configuration and content slots for one run of pages sharing a size and margin.
/// </summary>
/// <remarks>
/// Header and footer are drawn complete on every page and never paginate. Only <see cref="Body"/> flows,
/// which is what determines how many pages the run produces. Background and foreground ignore margins and cover
/// the whole sheet, making them the natural home for watermarks.
/// </remarks>
public sealed class Section
{
    public Extent Trim { get; set; } = PaperSizes.A4;

    public Sides Margins { get; set; } = Sides.All(0);

    public Ink Paper { get; set; } = Ink.White;

    public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;

    /// <summary>Style inherited by any text that does not override it.</summary>
    public TypeStyle DefaultType { get; set; } = TypeStyle.Default;

    /// <summary>
    /// When set, the page grows vertically to fit its content instead of using a fixed height, capped at the
    /// PDF maximum of 14400 points.
    /// </summary>
    public bool Continuous { get; set; }

    internal Frame RunningHeadSlot { get; } = new();

    internal Frame BodySlot { get; } = new();

    internal Frame RunningFootSlot { get; } = new();

    internal Frame UnderlaySlot { get; } = new();

    internal Frame OverlaySlot { get; } = new();

    public IFrame RunningHead() => RunningHeadSlot;

    public IFrame Body() => BodySlot;

    public IFrame RunningFoot() => RunningFootSlot;

    public IFrame Underlay() => UnderlaySlot;

    public IFrame Overlay() => OverlaySlot;

    internal IEnumerable<Block> Slots()
    {
        yield return RunningHeadSlot;
        yield return BodySlot;
        yield return RunningFootSlot;
        yield return UnderlaySlot;
        yield return OverlaySlot;
    }
}
