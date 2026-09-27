using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// Writes a <see cref="Gradient"/> as a shading pattern: an axial shading between two points, blended by a function
/// of the inks, that fills and strokes can be painted with.
/// </summary>
/// <remarks>
/// A pattern is placed in the page's own space, not the current drawing space, so its matrix is the drawing
/// transform in force when the gradient was set. Inks that are all process CMYK blend in CMYK; any other mix blends
/// in RGB, spot inks showing their fallback.
/// </remarks>
internal static class GradientPattern
{
    private static readonly PdfName PatternType = new PdfName("PatternType");
    private static readonly PdfName Shading = new PdfName("Shading");
    private static readonly PdfName ShadingType = new PdfName("ShadingType");
    private static readonly PdfName ColorSpace = new PdfName("ColorSpace");
    private static readonly PdfName Coords = new PdfName("Coords");
    private static readonly PdfName Function = new PdfName("Function");
    private static readonly PdfName Functions = new PdfName("Functions");
    private static readonly PdfName Extend = new PdfName("Extend");
    private static readonly PdfName Matrix = new PdfName("Matrix");
    private static readonly PdfName FunctionType = new PdfName("FunctionType");
    private static readonly PdfName Domain = new PdfName("Domain");
    private static readonly PdfName Bounds = new PdfName("Bounds");
    private static readonly PdfName Encode = new PdfName("Encode");
    private static readonly PdfName C0 = new PdfName("C0");
    private static readonly PdfName C1 = new PdfName("C1");
    private static readonly PdfName DeviceRgb = new PdfName("DeviceRGB");
    private static readonly PdfName DeviceCmyk = new PdfName("DeviceCMYK");

    /// <summary>The pattern dictionary for <paramref name="gradient"/> between two points of the drawing space.</summary>
    public static PdfDictionary Create(Gradient gradient, Offset start, Offset end, Transform placement)
    {
        IReadOnlyList<Ink> inks = gradient.Inks;
        bool cmyk = inks.All(ink => ink.Model == InkModel.Cmyk);

        PdfDictionary shading = new PdfDictionary
        {
            [ShadingType] = 2,
            [ColorSpace] = cmyk ? DeviceCmyk : DeviceRgb,
            [Coords] = new PdfArray(4) { start.X, start.Y, end.X, end.Y },
            [Function] = Blend(inks, cmyk),
            [Extend] = new PdfArray(2) { true, true },
        };

        return new PdfDictionary
        {
            [PatternType] = 2,
            [Shading] = shading,
            [Matrix] = new PdfArray(6) { placement.A, placement.B, placement.C, placement.D, placement.E, placement.F },
        };
    }

    /// <summary>
    /// A function from 0 to 1 onto the inks: one interpolation for two inks, or one per neighbouring pair stitched
    /// together at even intervals for more.
    /// </summary>
    private static PdfDictionary Blend(IReadOnlyList<Ink> inks, bool cmyk)
    {
        if (inks.Count == 2)
            return Between(inks[0], inks[1], cmyk);

        int pairs = inks.Count - 1;
        PdfArray functions = new PdfArray(pairs);
        PdfArray bounds = new PdfArray(pairs - 1);
        PdfArray encode = new PdfArray(pairs * 2);

        for (int index = 0; index < pairs; index++)
        {
            functions.Add(Between(inks[index], inks[index + 1], cmyk));
            encode.Add(0);
            encode.Add(1);

            if (index > 0)
                bounds.Add((double)index / pairs);
        }

        return new PdfDictionary
        {
            [FunctionType] = 3,
            [Domain] = new PdfArray(2) { 0, 1 },
            [Functions] = functions,
            [Bounds] = bounds,
            [Encode] = encode,
        };
    }

    private static PdfDictionary Between(Ink first, Ink second, bool cmyk) => new PdfDictionary
    {
        [FunctionType] = 2,
        [Domain] = new PdfArray(2) { 0, 1 },
        [C0] = Components(first, cmyk),
        [C1] = Components(second, cmyk),
        [PdfNames.N] = 1,
    };

    private static PdfArray Components(Ink ink, bool cmyk)
    {
        if (cmyk)
        {
            (float cyan, float magenta, float yellow, float black) = ink.ToCmyk();
            return new PdfArray(4) { cyan, magenta, yellow, black };
        }

        (float red, float green, float blue) = ink.ToRgb();
        return new PdfArray(3) { red, green, blue };
    }
}
