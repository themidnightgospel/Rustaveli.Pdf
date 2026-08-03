namespace Rustaveli.Pdf.Exceptions;

/// <summary>
/// Thrown when an element fails while drawing.
/// </summary>
public sealed class DocumentDrawingException(string message, Exception? innerException = null)
    : Exception(message, innerException);
