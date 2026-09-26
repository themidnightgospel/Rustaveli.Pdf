namespace Rustaveli.Pdf;

/// <summary>
/// Thrown when composing the document tree fails.
/// </summary>
public sealed class CompositionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
