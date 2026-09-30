using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Tagging;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Everything a block needs while drawing: the measurement services, the surface to draw onto, and the structure
/// elements it is drawn inside.
/// </summary>
internal sealed class RenderContext(ISurface surface, PlanContext layout, StructureElement? structure = null)
{
    public ISurface Surface { get; } = surface;

    public PlanContext Planning { get; } = layout;

    public Pagination Pagination => Planning.Pagination;

    public ITypeMeasurer Measurer => Planning.Measurer;

    /// <summary>Where every frame drawn is recorded, for a preview's inspector; null when not inspecting.</summary>
    public LayoutInspection? Inspection { get; init; }

    /// <summary>
    /// True while content is drawn ahead only to see where it would end (ADR 0016): what it would record about where it
    /// lands — an anchor's page, a captured position — is not where it will land, so it records nothing.
    /// </summary>
    public bool DrawsAhead { get; init; }

    /// <summary>The structure being drawn inside, when the output is tagged.</summary>
    public TagStack Tags { get; } = new TagStack(surface, structure);

    /// <summary>
    /// Draws <paramref name="child"/> in <paramref name="allotted"/> after measuring it in room
    /// <paramref name="measuredHeight"/> tall, keeping the difference in <see cref="PlanContext.RoomBelow"/>.
    /// </summary>
    public void RenderAllotted(Block child, Extent allotted, float measuredHeight) =>
        RenderWithRoomBelow(child, allotted, Planning.RoomBelow + Math.Max(0f, measuredHeight - allotted.Height));

    /// <summary>Draws <paramref name="child"/> in <paramref name="space"/> with <paramref name="roomBelow"/> in force.</summary>
    public void RenderWithRoomBelow(Block child, Extent space, float roomBelow)
    {
        float outer = Planning.RoomBelow;
        Planning.RoomBelow = roomBelow;

        try
        {
            child.Render(space, this);
        }
        finally
        {
            Planning.RoomBelow = outer;
        }
    }
}
