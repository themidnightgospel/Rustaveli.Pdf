namespace Rustaveli.Pdf;

/// <summary>
/// Thrown when laying out or drawing a page fails because content threw; the message names the page, and the inner
/// exception says why.
/// </summary>
public sealed class RenderingException(string message, Exception? innerException = null)
    : TypesettingException(message, innerException);
