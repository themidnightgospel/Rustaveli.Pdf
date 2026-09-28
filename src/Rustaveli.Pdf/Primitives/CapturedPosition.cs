namespace Rustaveli.Pdf;

/// <summary>Where captured content was drawn: on which page, where on it and how big a box it took.</summary>
/// <param name="Folio">The page's number, counting from one.</param>
/// <param name="Position">The top left of the content's box, in points from the page's top left.</param>
/// <param name="Size">The box the content took, before any scale or turn around it.</param>
public readonly record struct CapturedPosition(int Folio, Offset Position, Extent Size);
