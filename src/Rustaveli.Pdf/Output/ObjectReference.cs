using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>An annotation, <paramref name="Target"/>, on <paramref name="Page"/>, belonging to a structure element.</summary>
internal readonly record struct ObjectReference(PdfReference Target, PdfReference Page);
