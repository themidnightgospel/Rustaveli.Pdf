using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// The page dynamic content is being composed for: its number, the room left on it, and a way to measure content
/// before choosing it.
/// </summary>
public sealed class DynamicPage
{
    private readonly PlanContext _context;

    internal DynamicPage(PlanContext context, Extent room)
    {
        _context = context;
        Room = room;
    }

    /// <summary>The page's number and, once known, the page count.</summary>
    public PageFacts Facts =>
        new PageFacts(_context.Pagination.Folio, _context.Pagination.IsPageCountKnown ? _context.Pagination.PageCount : null);

    /// <summary>The room the content has on this page.</summary>
    public Extent Room { get; }

    /// <summary>The direction content reads in here.</summary>
    public ReadingDirection ReadingDirection => _context.ReadingDirection;

    /// <summary>The style text takes here when it sets none of its own.</summary>
    public TypeStyle DefaultType => _context.DefaultType;

    /// <summary>
    /// How big the content <paramref name="compose"/> makes would be in all the room there is, or null if it would
    /// not fit in it whole.
    /// </summary>
    public Extent? Measure(Action<IFrame> compose) => Measure(compose, Room);

    /// <summary>
    /// How big the content <paramref name="compose"/> makes would be in <paramref name="room"/>, or null if it would
    /// not fit whole.
    /// </summary>
    public Extent? Measure(Action<IFrame> compose, Extent room)
    {
        ArgumentNullException.ThrowIfNull(compose);

        Frame frame = new Frame();
        compose(frame);
        Fit plan = frame.Plan(room, _context);

        return plan.IsComplete || plan.IsNothing ? plan.Size : null;
    }

    /// <summary>Everywhere content captured under <paramref name="name"/> has been drawn.</summary>
    public IReadOnlyList<CapturedPosition> PositionsOf(string name) => _context.Pagination.PositionsOf(name);
}
