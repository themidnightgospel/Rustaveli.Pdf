namespace Rustaveli.Pdf;

/// <summary>How a stroke turns a corner.</summary>
public enum LineJoin
{
    /// <summary>A sharp point, cut off square beyond the miter limit.</summary>
    Miter,

    /// <summary>Rounded.</summary>
    Round,

    /// <summary>Cut off square at the corner.</summary>
    Bevel,
}
