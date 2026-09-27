namespace Rustaveli.Pdf;

/// <summary>What one step of a <see cref="VectorPath"/> does.</summary>
internal enum PathVerb
{
    /// <summary>Starts a figure at a point.</summary>
    Move,

    /// <summary>A straight segment to a point.</summary>
    Line,

    /// <summary>A cubic curve through two control points to a third.</summary>
    Cubic,

    /// <summary>Closes the figure back to where it started.</summary>
    Close,
}
