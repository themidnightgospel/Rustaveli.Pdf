using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

public enum SpacePlanType
{
    /// <summary>The element has nothing left to draw and occupies no space.</summary>
    Empty,

    /// <summary>The element cannot be drawn at all in the offered space and must be deferred to the next page.</summary>
    Wrap,

    /// <summary>The element drew what it could; the remainder continues on the next page.</summary>
    PartialRender,

    /// <summary>The element drew itself completely and has nothing left over.</summary>
    FullRender
}
