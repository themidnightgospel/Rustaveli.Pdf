using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Reading;

/// <summary>
/// A page of a file: its object, and its dictionary with the attributes it inherits from the page tree — resources,
/// boxes and rotation — written into it.
/// </summary>
internal sealed record SourcePage(PdfSource Source, int ObjectNumber, PdfDictionary Dictionary);
