namespace Rustaveli.Pdf.Layout;

internal enum FitKind
{
    /// <summary>The element has nothing left to draw and occupies no space.</summary>
    Nothing,

    /// <summary>The element cannot be drawn at all in the offered space and must be deferred to the next page.</summary>
    Defer,

    /// <summary>The element drew what it could; the remainder continues on the next page.</summary>
    Partial,

    /// <summary>The element drew itself completely and has nothing left over.</summary>
    Complete
}
