namespace Rustaveli.Pdf.Images;

/// <summary>
/// How the stored pixels must be transformed to appear upright, as recorded by the EXIF Orientation tag (0x0112).
/// Values match the tag's numbering.
/// </summary>
/// <remarks>
/// PDF viewers ignore EXIF, so an image is embedded exactly as stored and the layout applies the transform. For
/// the four values that include a quarter turn the upright image is <see cref="Rustaveli.Pdf.Drawing.IImage.PixelHeight"/>
/// wide and <see cref="Rustaveli.Pdf.Drawing.IImage.PixelWidth"/> tall.
/// </remarks>
internal enum ExifOrientation
{
    /// <summary>Stored upright; also the value for a missing, unreadable or out-of-range tag.</summary>
    Normal = 1,

    /// <summary>Mirrored left to right.</summary>
    FlipHorizontal = 2,

    /// <summary>Upside down.</summary>
    Rotate180 = 3,

    /// <summary>Mirrored top to bottom.</summary>
    FlipVertical = 4,

    /// <summary>Mirrored across the top-left to bottom-right diagonal.</summary>
    Transpose = 5,

    /// <summary>Must be turned 90 degrees clockwise to appear upright.</summary>
    Rotate90 = 6,

    /// <summary>Mirrored across the top-right to bottom-left diagonal.</summary>
    Transverse = 7,

    /// <summary>Must be turned 90 degrees counter-clockwise (270 clockwise) to appear upright.</summary>
    Rotate270 = 8,
}
