using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>Content marked <paramref name="Identifier"/> on <paramref name="Page"/>, drawn for a structure element.</summary>
internal readonly record struct MarkedContentReference(PdfReference Page, int Identifier);
