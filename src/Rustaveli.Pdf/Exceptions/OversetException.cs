namespace Rustaveli.Pdf;

/// <summary>
/// Thrown when a document tree imposes constraints that no page size could satisfy.
/// </summary>
/// <remarks>
/// The engine responds to content that does not fit by deferring it to the next page. If a fresh, entirely empty
/// page still cannot accommodate it, no further page ever will, and continuing would loop forever. This
/// exception reports that condition along with the element responsible.
/// </remarks>
public sealed class OversetException(string message) : Exception(message);
