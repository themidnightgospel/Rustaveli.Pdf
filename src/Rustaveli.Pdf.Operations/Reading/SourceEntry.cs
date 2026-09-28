namespace Rustaveli.Pdf.Operations.Reading;

/// <summary>
/// Where a file keeps one object: at a byte offset, or as the <paramref name="Index"/>th object of the object stream
/// numbered <paramref name="Stream"/>.
/// </summary>
internal readonly record struct SourceEntry(long Offset, int Stream, int Index)
{
    public static SourceEntry At(long offset) => new SourceEntry(offset, 0, 0);

    public static SourceEntry InStream(int stream, int index) => new SourceEntry(0, stream, index);

    public bool IsCompressed => Stream > 0;
}
