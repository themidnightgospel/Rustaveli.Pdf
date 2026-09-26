namespace Rustaveli.Pdf;

/// <summary>
/// A place content is set into. Sections, composers and modifiers hand out frames, each holding one piece of
/// content; every modifier returns the frame inside it, so modifiers chain until content ends the chain.
/// </summary>
/// <remarks>
/// Only the library makes frames. To package composition of your own, implement <see cref="ISnippet"/>.
/// </remarks>
public interface IFrame
{
}
