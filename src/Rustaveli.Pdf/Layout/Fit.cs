namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The outcome of planning a block in a room: whether it draws there, how much of it, and how much room it takes.
/// </summary>
/// <remarks>
/// Planning is asked many times over, speculatively, before anything is drawn, so an outcome is a plain value that
/// parents compare, pass on and resize freely. Only a Partial or Complete outcome has a size; Nothing and Defer take
/// no room, and only Defer says why, so the reason can travel up to the message a failed layout ends with.
/// </remarks>
internal readonly record struct Fit
{
    private Fit(FitKind kind, Extent size, string? deferReason)
    {
        Kind = kind;
        Size = size;
        DeferReason = deferReason;
    }

    public FitKind Kind { get; }

    /// <summary>The room the block takes: zero unless it draws something.</summary>
    public Extent Size { get; }

    /// <summary>Why the block cannot be drawn in the room it was offered; null for every outcome but Defer.</summary>
    public string? DeferReason { get; }

    public bool IsNothing => Kind == FitKind.Nothing;

    public bool IsDeferred => Kind == FitKind.Defer;

    public bool IsPartial => Kind == FitKind.Partial;

    public bool IsComplete => Kind == FitKind.Complete;

    /// <summary>Whether the block draws something in the room, whether or not more of it remains.</summary>
    public bool PlacesContent => Kind is FitKind.Partial or FitKind.Complete;

    public static Fit Nothing() => new Fit(FitKind.Nothing, Extent.Zero, null);

    public static Fit Defer(string reason) => new Fit(FitKind.Defer, Extent.Zero, reason);

    public static Fit Partial(Extent size) => new Fit(FitKind.Partial, size, null);

    public static Fit Partial(float width, float height) => Partial(new Extent(width, height));

    public static Fit Complete(Extent size) => new Fit(FitKind.Complete, size, null);

    public static Fit Complete(float width, float height) => Complete(new Extent(width, height));
}
