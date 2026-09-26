namespace Rustaveli.Pdf.Exceptions;

/// <summary>
/// Thrown when an element fails while drawing.
/// </summary>
public sealed class RenderingException(string message, Exception? innerException = null)
    : Exception(message, innerException);
