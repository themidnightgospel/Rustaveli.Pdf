using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The result of measuring an element against a given amount of space.
/// </summary>
/// <remarks>
/// The four outcomes drive pagination. <see cref="FitKind.Defer" /> asks the engine to retry on a fresh
/// page; <see cref="FitKind.Partial" /> tells it to draw now and come back for the rest. An element
/// that returns <see cref="FitKind.Defer" /> on a page that is already empty cannot ever fit, which is how
/// the engine detects a non-terminating layout instead of looping forever.
/// </remarks>
public readonly record struct Fit
{
    public FitKind Kind { get; }

    /// <summary>The space the element will occupy. Always <see cref="Extent.Zero" /> for <see cref="FitKind.Defer" />.</summary>

    public Extent Size { get; }

    /// <summary>Explains why the element could not be drawn. Populated only for <see cref="FitKind.Defer" />.</summary>
    public string? DeferReason { get; }

    public bool IsDeferred => Kind == FitKind.Defer;

    public bool IsNothing => Kind == FitKind.Nothing;

    public bool IsComplete => Kind == FitKind.Complete;

    public bool IsPartial => Kind == FitKind.Partial;

    /// <summary>True when the element produced geometry this pass, whether or not anything remains.</summary>
    public bool PlacesContent
    {
        get
        {
            FitKind type = Kind;
            if ((uint)(type - 2) <= 1u)
            {
                return true;
            }
            return false;
        }
    }

    private Fit(FitKind type, Extent size, string? wrapReason)
    {
        Kind = type;
        Size = size;
        DeferReason = wrapReason;
    }

    public static Fit Nothing()
    {
        return new Fit(FitKind.Nothing, Extent.Zero, null);
    }

    public static Fit Defer(string reason)
    {
        return new Fit(FitKind.Defer, Extent.Zero, reason);
    }

    public static Fit Partial(Extent size)
    {
        return new Fit(FitKind.Partial, size, null);
    }

    public static Fit Partial(float width, float height)
    {
        return Partial(new Extent(width, height));
    }

    public static Fit Complete(Extent size)
    {
        return new Fit(FitKind.Complete, size, null);
    }

    public static Fit Complete(float width, float height)
    {
        return Complete(new Extent(width, height));
    }

    public override string ToString() => Kind switch
    {
        FitKind.Defer => $"Defer ({DeferReason})",
        FitKind.Nothing => "Nothing",
        _ => $"{Kind} {Size}"
    };
}
