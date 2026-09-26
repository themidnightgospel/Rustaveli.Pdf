namespace Rustaveli.Pdf.Images;

/// <summary>
/// The /DecodeParms of a /FlateDecode image stream whose rows carry PNG filter-type bytes: a reader inflates the
/// data, then undoes the per-row PNG prediction.
/// </summary>
internal sealed class FlateDecodeParameters(int colors, int bitsPerComponent, int columns)
{
    /// <summary>
    /// Predictor 15, "PNG prediction, optimum": each row starts with its own filter-type byte, exactly as PNG stores
    /// it. (Values 10–14 would also be read per row; 15 is the conventional value for mixed filters.)
    /// </summary>
    public int Predictor => 15;

    /// <summary>Samples per pixel.</summary>
    public int Colors { get; } = colors;

    /// <summary>Bits per sample.</summary>
    public int BitsPerComponent { get; } = bitsPerComponent;

    /// <summary>Pixels per row.</summary>
    public int Columns { get; } = columns;
}
