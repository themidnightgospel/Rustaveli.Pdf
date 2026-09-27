namespace Rustaveli.Pdf.Images;

/// <summary>Receives a decoded PNG's rows, top to bottom, one call per row.</summary>
internal interface IPngRowSink
{
    /// <summary>
    /// Takes one unfiltered, de-interlaced row in the PNG's own sample layout: samples big-endian, packed several
    /// to a byte below 8 bits, alpha interleaved. The span is only valid for the duration of the call.
    /// </summary>
    void Accept(ReadOnlySpan<byte> row);
}
