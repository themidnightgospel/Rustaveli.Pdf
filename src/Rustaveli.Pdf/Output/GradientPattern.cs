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
    private static readonly PdfName Coords = new PdfName("Coords");
    private static readonly PdfName Function = new PdfName("Function");
    private static readonly PdfName Functions = new PdfName("Functions");
    private static readonly PdfName Extend = new PdfName("Extend");
    private static readonly PdfName Matrix = new PdfName("Matrix");
    private static readonly PdfName Bounds = new PdfName("Bounds");
    private static readonly PdfName Encode = new PdfName("Encode");

    /// <summary>The pattern dictionary for <paramref name="gradient"/> between two points of the drawing space.</summary>
    public static PdfDictionary Create(Gradient gradient, Offset start, Offset end, Transform placement, bool rgbOnly)
    {
        IReadOnlyList<Ink> inks = gradient.Inks;
        bool cmyk = !rgbOnly && inks.All(ink => ink.Model == InkModel.Cmyk);

        PdfDictionary shading = new PdfDictionary
        {
            [ShadingType] = 2,
            [PdfNames.ColorSpace] = cmyk ? PdfNames.DeviceCMYK : PdfNames.DeviceRGB,
            [Coords] = new PdfArray(4) { start.X, start.Y, end.X, end.Y },
            [Function] = Blend(inks, gradient.Positions, cmyk),
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
    /// A function from 0 to 1 onto the inks at their positions: one interpolation for two inks at the ends, or one
    /// per neighbouring pair stitched together where each ink lies. Before the first ink and after the last, the blend
    /// holds that ink.
    /// </summary>
    private static PdfDictionary Blend(IReadOnlyList<Ink> inks, IReadOnlyList<float> positions, bool cmyk)
    {
        List<(float Position, Ink Ink)> stops = inks.Select((ink, index) => (positions[index], ink)).ToList();

        if (stops[0].Position > 0)
            stops.Insert(0, (0f, stops[0].Ink));

        if (stops[stops.Count - 1].Position < 1)
            stops.Add((1f, stops[stops.Count - 1].Ink));

        if (stops.Count == 2)
            return Between(stops[0].Ink, stops[1].Ink, cmyk);

        int pairs = stops.Count - 1;
        PdfArray functions = new PdfArray(pairs);
        PdfArray bounds = new PdfArray(pairs - 1);
        PdfArray encode = new PdfArray(pairs * 2);

        for (int index = 0; index < pairs; index++)
        {
            functions.Add(Between(stops[index].Ink, stops[index + 1].Ink, cmyk));
            encode.Add(0);
            encode.Add(1);

            if (index > 0)
                bounds.Add(stops[index].Position);
        }

        return new PdfDictionary
        {
            [PdfNames.FunctionType] = 3,
            [PdfNames.Domain] = new PdfArray(2) { 0, 1 },
            [Functions] = functions,
            [Bounds] = bounds,
            [Encode] = encode,
        };
    }

    private static PdfDictionary Between(Ink first, Ink second, bool cmyk) =>
        InterpolationFunction.Create(Components(first, cmyk), Components(second, cmyk));

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
