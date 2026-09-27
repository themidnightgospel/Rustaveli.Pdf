namespace Rustaveli.Pdf.Blocks;

/// <summary>Where the lines of a flow sit across its width, and how the gaps in them are spread.</summary>
internal enum FlowPlacement
{
    /// <summary>Against the start, gaps as set.</summary>
    Left,

    /// <summary>Centred, gaps as set.</summary>
    Center,

    /// <summary>Against the end, gaps as set.</summary>
    Right,

    /// <summary>Across the whole width, the extra room shared between the gaps; the last line is not.</summary>
    Justify,

    /// <summary>Across the whole width, the extra room shared out around every item.</summary>
    SpaceAround,
}
