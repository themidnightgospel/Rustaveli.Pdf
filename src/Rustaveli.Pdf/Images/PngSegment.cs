namespace Rustaveli.Pdf.Images;

/// <summary>The location of one IDAT chunk's payload within a PNG file.</summary>
internal readonly struct PngSegment(int offset, int length)
{
    public int Offset { get; } = offset;

    public int Length { get; } = length;
}
