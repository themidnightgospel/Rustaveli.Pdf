namespace Rustaveli.Pdf.Exceptions;

/// <summary>
/// Thrown when composing the document tree fails.
/// </summary>
public sealed class DocumentComposeException(string message, Exception? innerException = null)
    : Exception(message, innerException);
