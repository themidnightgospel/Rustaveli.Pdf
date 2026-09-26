namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// How an image is scaled into the space allocated to it.
/// </summary>
public enum ImageFitting
{
    /// <summary>Fills the available width, deriving height from the aspect ratio.</summary>
    FitWidth,

    /// <summary>Fills the available height, deriving width from the aspect ratio.</summary>
    FitHeight,

    /// <summary>Fits entirely within the available area, preserving the aspect ratio.</summary>
    Proportionally,

    /// <summary>Fills the available area exactly, distorting the image.</summary>
    Stretch
}
