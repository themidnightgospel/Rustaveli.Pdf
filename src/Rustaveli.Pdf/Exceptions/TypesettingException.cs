namespace Rustaveli.Pdf;

/// <summary>
/// The base of every failure the library reports while composing, setting or rendering a document, so that one
/// catch handles them all.
/// </summary>
/// <remarks>
/// <see cref="CompositionException"/> means the document was put together wrongly, <see cref="OversetException"/>
/// that some content fits on no page, and <see cref="RenderingException"/> that drawing a page failed.
/// </remarks>
public abstract class TypesettingException : Exception
{
    private protected TypesettingException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
