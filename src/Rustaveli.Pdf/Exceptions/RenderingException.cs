namespace Rustaveli.Pdf;

/// <summary>
/// Thrown when drawing a page fails; the inner exception says why.
/// </summary>
public sealed class RenderingException(string message, Exception? innerException = null)
    : TypesettingException(message, innerException);
