namespace Rustaveli.Pdf;

/// <summary>
/// Thrown when content fits on no page. In print, text that does not fit its frame is <em>overset</em>.
/// </summary>
/// <remarks>
/// Content that does not fit is deferred to the next page. If a fresh, empty page still cannot hold it, no later
/// page ever will, and continuing would loop forever; this exception reports that instead, with the reason.
/// </remarks>
public sealed class OversetException(string message) : TypesettingException(message, null);
