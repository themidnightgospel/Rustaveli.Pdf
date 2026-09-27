namespace Rustaveli.Pdf.Writing;

/// <summary>The shape at the corners of stroked paths (ISO 32000-1, 8.4.3.4).</summary>
internal enum PdfLineJoin
{
    Miter = 0,
    Round = 1,
    Bevel = 2,
}
