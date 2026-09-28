using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Reading;

/// <summary>A stream read from a file: its dictionary, and its data still encoded as the file holds it.</summary>
internal sealed record SourceStream(PdfDictionary Dictionary, byte[] Data);
