namespace Rustaveli.Pdf.Layout;

/// <summary>
/// A block holding at most one child, which by default it plans and draws exactly as the child would be on its own.
/// Blocks that add one thing to their content — an inset, a scale, a condition — derive from it and override only
/// what they change.
/// </summary>
/// <remarks>
/// It is also the engine's side of a frame: the one block a frame holds is its <see cref="Child"/>.
/// </remarks>
internal abstract class EnclosingBlock : Block, IFrameSlot
{
    /// <summary>The content, or null while nothing has been placed here.</summary>
    public Block? Child { get; set; }

    /// <summary>
    /// Whatever the content says, so that a fill or stroke around content drawn again on every page is drawn with it.
    /// </summary>
    internal override bool Repeats => Child?.Repeats ?? false;

    public override IEnumerable<Block?> GetChildren()
    {
        yield return Child;
    }

    /// <summary>
    /// The child's plan for the same room. With no child the block is Complete and takes no room: an absent child is
    /// finished from the start, whereas Nothing belongs to content that has been drawn and used up.
    /// </summary>
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Child is null ? Fit.Complete(Extent.Zero) : Child.Plan(availableSpace, context);

    protected override void RenderCore(Extent availableSpace, RenderContext context) =>
        Child?.Render(availableSpace, context);

    /// <summary>
    /// <paramref name="plan"/> reporting <paramref name="size"/> instead of its own, for a block that changes how much
    /// room its content takes but not whether the content draws: Defer and Nothing are returned as they are, since
    /// neither takes room, and Partial and Complete keep their kind.
    /// </summary>
    protected static Fit Resized(Fit plan, Extent size) => plan.Kind switch
    {
        FitKind.Partial => Fit.Partial(size),
        FitKind.Complete => Fit.Complete(size),
        _ => plan,
    };
}
