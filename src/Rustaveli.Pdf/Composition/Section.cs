using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

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

    /// <summary>The smallest a page of this section may be.</summary>
    internal Extent SmallestTrim => throw new NotImplementedException("To be written anew from its specification.");

    internal Extent LargestTrim => throw new NotImplementedException("To be written anew from its specification.");

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
