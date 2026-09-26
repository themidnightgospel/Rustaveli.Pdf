namespace Rustaveli.Pdf.Images;

/// <summary>
/// A PNG's chunks as <see cref="PngParser"/> validated them: the header, the palette and transparency, where the
/// image data lies, and the colour metadata. The image data itself is not decompressed here.
/// </summary>
internal sealed class PngFile
{
    public PngFile(
        ReadOnlyMemory<byte> source,
        PngHeader header,
        IReadOnlyList<PngSegment> imageData,
        ReadOnlyMemory<byte> palette,
        ReadOnlyMemory<byte> paletteAlpha,
        IReadOnlyList<int>? transparentColor,
        ImageMetadata metadata)
    {
        Source = source;
        Header = header;
        ImageData = imageData;
        Palette = palette;
        PaletteAlpha = paletteAlpha;
        TransparentColor = transparentColor;
        Metadata = metadata;

        long length = 0;
        foreach (PngSegment segment in imageData)
            length += segment.Length;
        ImageDataLength = length;
    }

    /// <summary>The complete file.</summary>
    public ReadOnlyMemory<byte> Source { get; }

    public PngHeader Header { get; }

    /// <summary>Where the payloads of the IDAT chunks lie in <see cref="Source"/>, in order.</summary>
    public IReadOnlyList<PngSegment> ImageData { get; }

    /// <summary>The combined length of the IDAT payloads: the length of the zlib stream they form.</summary>
    public long ImageDataLength { get; }

    /// <summary>The PLTE entries, three bytes (R, G, B) each, for a palette image; empty otherwise.</summary>
    public ReadOnlyMemory<byte> Palette { get; }

    /// <summary>
    /// For a palette image with a tRNS chunk: the alpha of palette entries 0, 1, …; entries beyond its length are
    /// opaque. Empty otherwise.
    /// </summary>
    public ReadOnlyMemory<byte> PaletteAlpha { get; }

    /// <summary>
    /// For a gray or RGB image with a tRNS chunk: the one sample value (gray) or three (RGB) that marks a pixel fully
    /// transparent. Null otherwise.
    /// </summary>
    public IReadOnlyList<int>? TransparentColor { get; }

    public ImageMetadata Metadata { get; }

    /// <summary>Copies the IDAT payloads into one contiguous zlib stream.</summary>
    /// <remarks>
    /// With a single IDAT chunk — every small image, and many large ones — the stream already lies contiguously in
    /// the file, so it is returned without a copy.
    /// </remarks>
    public ReadOnlyMemory<byte> ConcatenateImageData()
    {
        if (ImageData.Count == 1)
            return Source.Slice(ImageData[0].Offset, ImageData[0].Length);

        byte[] data = new byte[ImageDataLength];
        CopyImageData(data);
        return data;
    }

    /// <summary>Copies the IDAT payloads, in order, to the start of <paramref name="destination"/>.</summary>
    public void CopyImageData(Span<byte> destination)
    {
        int position = 0;
        ReadOnlySpan<byte> source = Source.Span;
        foreach (PngSegment segment in ImageData)
        {
            source.Slice(segment.Offset, segment.Length).CopyTo(destination.Slice(position));
            position += segment.Length;
        }
    }
}
