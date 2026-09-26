namespace Rustaveli.Pdf.Images;

/// <summary>
/// Thrown when image data is malformed, truncated, or exceeds the limits the loader accepts.
/// </summary>
/// <remarks>
/// This is the only exception the image layer lets escape for bad input. Parsers read attacker-controlled bytes,
/// so every structural check reports through this type rather than surfacing as an index, overflow or I/O error
/// that a caller could not tell apart from a defect in the library.
/// </remarks>
internal class ImageFormatException : Exception
{
    public ImageFormatException(string message)
        : base(message)
    {
    }

    public ImageFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
