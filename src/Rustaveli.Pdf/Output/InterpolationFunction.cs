using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// A type 2 function: over 0 to 1, a straight blend from one set of colour components to another.
/// </summary>
internal static class InterpolationFunction
{
    /// <summary>The function giving <paramref name="from"/> at 0 and <paramref name="to"/> at 1.</summary>
    public static PdfDictionary Create(PdfArray from, PdfArray to) => new PdfDictionary
    {
        [PdfNames.FunctionType] = 2,
        [PdfNames.Domain] = new PdfArray(2) { 0, 1 },
        [PdfNames.C0] = from,
        [PdfNames.C1] = to,
        [PdfNames.N] = 1,
    };
}
