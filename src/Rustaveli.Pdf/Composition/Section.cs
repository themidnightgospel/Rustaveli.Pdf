using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// A run of pages that share a size, margins, paper and running bands, and the five frames their content goes into.
/// </summary>
/// <remarks>
/// <para>
/// Only the body flows: it is set page after page until it is used up, and it alone decides how many pages the
/// section takes. The running head and running foot are composed once and drawn whole on every page, inside the
/// margins, the head at the top of the room they leave and the foot against the bottom margin, with the body between
/// them. The underlay and the overlay are drawn on every page too, across the whole sheet from its corner, margins
/// or not: the underlay beneath everything else, the place for a background, and the overlay above everything else,
/// the place for a stamp over the text. The paper is painted across the whole sheet first.
/// </para>
/// <para>
/// Sizes are checked where they are set, so a page size no PDF can hold fails at the line that sets it rather than
/// when the document is laid out.
/// </para>
/// </remarks>
public sealed class Section
{
    private Extent _trim = PaperSizes.A4;
    private Extent? _minimumTrim;
    private Extent? _maximumTrim;
    private Sides _margins = Sides.All(0);

    /// <summary>
    /// The size of the section's pages, in points: greater than nothing each way, and at most 14,400 points, the
    /// largest page a PDF allows.
    /// </summary>
    public Extent Trim
    {
        get => _trim;
        set => _trim = PageSize(value, nameof(Trim), allowNothing: false);
    }

    /// <summary>The space left blank around each page's content, in points: finite, and none negative.</summary>
    public Sides Margins
    {
        get => _margins;
        set
        {
            if (!IsLength(value.Left) || !IsLength(value.Top) || !IsLength(value.Right) || !IsLength(value.Bottom))
                throw new ArgumentOutOfRangeException(nameof(Margins), value, "Margins are finite numbers of points, none negative.");

            _margins = value;
        }
    }

    public Ink Paper { get; set; } = Ink.White;

    public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;

    /// <summary>Style inherited by any text that does not override it.</summary>
    public TypeStyle DefaultType { get; set; } = TypeStyle.Default;

    /// <summary>
    /// Whether the section is set as one long page: as wide as <see cref="Trim"/>, and as tall as its content, from
    /// nothing up to the 14,400 points a PDF page may be. The margins and the running bands count within that height.
    /// </summary>
    public bool Continuous { get; set; }

    /// <summary>
    /// When set, the smallest a page may be: pages are sized by their content, no smaller than this and no larger
    /// than <see cref="MaximumTrim"/>, or <see cref="Trim"/> when that is not set. Nothing or more each way, and at
    /// most 14,400 points.
    /// </summary>
    public Extent? MinimumTrim
    {
        get => _minimumTrim;
        set => _minimumTrim = value is { } size ? PageSize(size, nameof(MinimumTrim), allowNothing: true) : null;
    }

    /// <summary>
    /// When set, the largest a page may be: pages are sized by their content, no larger than this and no smaller
    /// than <see cref="MinimumTrim"/>, or <see cref="Trim"/> when that is not set. Content that does not fit flows
    /// on to a page of the same bounds. Greater than nothing each way, and at most 14,400 points.
    /// </summary>
    public Extent? MaximumTrim
    {
        get => _maximumTrim;
        set => _maximumTrim = value is { } size ? PageSize(size, nameof(MaximumTrim), allowNothing: false) : null;
    }

    /// <summary>
    /// The smallest a page of this section may be: the minimum when one is set, otherwise the trim — for a continuous
    /// section only its width, since its height starts from nothing.
    /// </summary>
    internal Extent SmallestTrim => MinimumTrim ?? (Continuous ? new Extent(Trim.Width, 0) : Trim);

    /// <summary>
    /// The largest a page of this section may be: the maximum when one is set, otherwise the trim — for a continuous
    /// section as tall as a PDF page may be.
    /// </summary>
    internal Extent LargestTrim => MaximumTrim ?? (Continuous ? new Extent(Trim.Width, Extent.Max.Height) : Trim);

    internal Frame RunningHeadSlot { get; } = new();

    internal Frame BodySlot { get; } = new();

    internal Frame RunningFootSlot { get; } = new();

    internal Frame UnderlaySlot { get; } = new();

    internal Frame OverlaySlot { get; } = new();

    /// <summary>The frame drawn whole at the top of every page, inside the margins.</summary>
    public IFrame RunningHead() => RunningHeadSlot;

    /// <summary>The frame whose content flows from page to page, between the running head and foot.</summary>
    public IFrame Body() => BodySlot;

    /// <summary>The frame drawn whole at the foot of every page, against the bottom margin.</summary>
    public IFrame RunningFoot() => RunningFootSlot;

    /// <summary>The frame drawn beneath everything else on every page, across the whole sheet.</summary>
    public IFrame Underlay() => UnderlaySlot;

    /// <summary>The frame drawn above everything else on every page, across the whole sheet.</summary>
    public IFrame Overlay() => OverlaySlot;

    /// <summary>
    /// <paramref name="size"/>, checked where it is set rather than when the pages are laid out: finite, at most the
    /// 14,400 points a PDF page may be each way, and greater than nothing unless <paramref name="allowNothing"/>.
    /// </summary>
    private static Extent PageSize(Extent size, string name, bool allowNothing)
    {
        if (!Fits(size.Width, Extent.Max.Width) || !Fits(size.Height, Extent.Max.Height))
        {
            throw new ArgumentOutOfRangeException(name, size, allowNothing
                ? "A page size is from nothing to 14,400 points each way, the largest page a PDF allows."
                : "A page size is greater than nothing and at most 14,400 points each way, the largest page a PDF allows.");
        }

        return size;

        // NaN compares false either way, so it never fits.
        bool Fits(float length, float largest) => (allowNothing ? length >= 0 : length > 0) && length <= largest;
    }

    private static bool IsLength(float value) => value >= 0 && !float.IsInfinity(value);
}
