using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The result of measuring an element against a given amount of space.
/// </summary>
/// <remarks>
/// The four outcomes drive pagination. <see cref="FitKind.Wrap" /> asks the engine to retry on a fresh
/// page; <see cref="FitKind.PartialRender" /> tells it to draw now and come back for the rest. An element
/// that returns <see cref="FitKind.Wrap" /> on a page that is already empty cannot ever fit, which is how
/// the engine detects a non-terminating layout instead of looping forever.
/// </remarks>
public readonly record struct Fit
{
    public FitKind Type { get; }

    /// <summary>The space the element will occupy. Always <see cref="Extent.Zero" /> for <see cref="FitKind.Wrap" />.</summary>

    public Extent Size { get; }

    /// <summary>Explains why the element could not be drawn. Populated only for <see cref="FitKind.Wrap" />.</summary>
    public string? WrapReason { get; }

    public bool IsWrap => Type == FitKind.Wrap;

    public bool IsEmpty => Type == FitKind.Empty;

    public bool IsFullRender => Type == FitKind.FullRender;

    public bool IsPartialRender => Type == FitKind.PartialRender;

    /// <summary>True when the element produced geometry this pass, whether or not anything remains.</summary>
    public bool DrewSomething
    {
        get
        {
            FitKind type = Type;
            if ((uint)(type - 2) <= 1u)
            {
                return true;
            }
            return false;
        }
    }

    private Fit(FitKind type, Extent size, string? wrapReason)
    {
        Type = type;
        Size = size;
        WrapReason = wrapReason;
    }

    public static Fit Empty()
    {
        return new Fit(FitKind.Empty, Extent.Zero, null);
    }

    public static Fit Wrap(string reason)
    {
        return new Fit(FitKind.Wrap, Extent.Zero, reason);
    }

    public static Fit PartialRender(Extent size)
    {
        return new Fit(FitKind.PartialRender, size, null);
    }

    public static Fit PartialRender(float width, float height)
    {
        return PartialRender(new Extent(width, height));
    }

    public static Fit FullRender(Extent size)
    {
        return new Fit(FitKind.FullRender, size, null);
    }

    public static Fit FullRender(float width, float height)
    {
        return FullRender(new Extent(width, height));
    }

    public override string ToString() => Type switch
    {
        FitKind.Wrap => $"Wrap ({WrapReason})",
        FitKind.Empty => "Empty",
        _ => $"{Type} {Size}"
    };
}
