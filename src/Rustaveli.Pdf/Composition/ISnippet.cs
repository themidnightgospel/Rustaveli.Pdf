namespace Rustaveli.Pdf;

/// <summary>
/// A piece of composing packaged for reuse: a heading style, an address block, a table layout used on many pages.
/// </summary>
/// <remarks>
/// Applied with <see cref="FrameContent.Snippet(IFrame, ISnippet)"/>, a snippet composes into the frame it is given
/// exactly as the same code written out in place would. Only the library makes frames, so this, rather than a frame of
/// one's own, is how composing is packaged outside it.
/// </remarks>
public interface ISnippet
{
    /// <summary>Composes the snippet's content into <paramref name="frame"/>.</summary>
    void Compose(IFrame frame);
}
