using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The result of measuring an element against a given amount of space.
/// </summary>
/// <remarks>
/// The four outcomes drive pagination. <see cref="SpacePlanType.Wrap" /> asks the engine to retry on a fresh
/// page; <see cref="SpacePlanType.PartialRender" /> tells it to draw now and come back for the rest. An element
/// that returns <see cref="SpacePlanType.Wrap" /> on a page that is already empty cannot ever fit, which is how
/// the engine detects a non-terminating layout instead of looping forever.
/// </remarks>
public readonly record struct SpacePlan
{
    public SpacePlanType Type { get; }

    /// <summary>The space the element will occupy. Always <see cref="Size.Zero" /> for <see cref="SpacePlanType.Wrap" />.</summary>
    public Size Size { get; }

    /// <summary>Explains why the element could not be drawn. Populated only for <see cref="SpacePlanType.Wrap" />.</summary>
    public string? WrapReason { get; }

    public bool IsWrap => Type == SpacePlanType.Wrap;

    public bool IsEmpty => Type == SpacePlanType.Empty;

    public bool IsFullRender => Type == SpacePlanType.FullRender;

    public bool IsPartialRender => Type == SpacePlanType.PartialRender;

    /// <summary>True when the element produced geometry this pass, whether or not anything remains.</summary>
    public bool DrewSomething
    {
        get
        {
            SpacePlanType type = Type;
            if ((uint)(type - 2) <= 1u)
            {
                return true;
            }
            return false;
        }
    }

    private SpacePlan(SpacePlanType type, Size size, string? wrapReason)
    {
        Type = type;
        Size = size;
        WrapReason = wrapReason;
    }

    public static SpacePlan Empty()
    {
        return new SpacePlan(SpacePlanType.Empty, Size.Zero, null);
    }

    public static SpacePlan Wrap(string reason)
    {
        return new SpacePlan(SpacePlanType.Wrap, Size.Zero, reason);
    }

    public static SpacePlan PartialRender(Size size)
    {
        return new SpacePlan(SpacePlanType.PartialRender, size, null);
    }

    public static SpacePlan PartialRender(float width, float height)
    {
        return PartialRender(new Size(width, height));
    }

    public static SpacePlan FullRender(Size size)
    {
        return new SpacePlan(SpacePlanType.FullRender, size, null);
    }

    public static SpacePlan FullRender(float width, float height)
    {
        return FullRender(new Size(width, height));
    }

    public override string ToString() => Type switch
    {
        SpacePlanType.Wrap => $"Wrap ({WrapReason})",
        SpacePlanType.Empty => "Empty",
        _ => $"{Type} {Size}"
    };
}
