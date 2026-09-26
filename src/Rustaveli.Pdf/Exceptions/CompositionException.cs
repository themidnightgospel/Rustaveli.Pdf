namespace Rustaveli.Pdf;

/// <summary>
/// Thrown when a document is composed wrongly: two pieces of content set into one frame, a modifier applied
/// where it has no meaning, a table cell outside its columns, or an exception thrown by the composing code itself.
/// </summary>
public sealed class CompositionException(string message, Exception? innerException = null)
    : TypesettingException(message, innerException);
