using System.Buffers.Binary;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Recognises an image format from its leading bytes. Never throws: anything unrecognised is
/// <see cref="ImageFormat.Unknown"/>.
/// </summary>
/// <remarks>
/// Detection looks at signatures only. It does not validate the rest of the file — that is the parser's job — so a
/// truncated PNG is still reported as <see cref="ImageFormat.Png"/> and then rejected with a precise message.
/// </remarks>
internal static class ImageFormatDetector
{
    private const int MaxBrandBytes = 256;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static ImageFormat Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ImageFormat.Jpeg;

        if (data.StartsWith(PngSignature))
            return ImageFormat.Png;

        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
            return ImageFormat.Gif;

        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ImageFormat.WebP;

        // Classic TIFF carries 42 after the byte-order mark, BigTIFF 43.
        if (data.StartsWith("II*\0"u8) || data.StartsWith("MM\0*"u8) ||
            data.StartsWith("II+\0"u8) || data.StartsWith("MM\0+"u8))
            return ImageFormat.Tiff;

        if (data.Length >= 12 && data.Slice(4, 4).SequenceEqual("ftyp"u8))
            return DetectIsoMedia(data);

        if (IsBitmap(data))
            return ImageFormat.Bmp;

        return ImageFormat.Unknown;
    }

    // "BM" alone is too weak a signature — plenty of text starts with it — so the size of the header that follows
    // the 14-byte file header must also be one of the sizes the format defines.
    private static bool IsBitmap(ReadOnlySpan<byte> data)
    {
        if (data.Length < 18 || data[0] != (byte)'B' || data[1] != (byte)'M')
            return false;

        uint infoHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(14, 4));
        return infoHeaderSize is 12 or 40 or 52 or 56 or 64 or 108 or 124;
    }

    // HEIC and AVIF share the ISO base media file container; the brands in the leading 'ftyp' box say which codec
    // the file holds. The major brand is checked first, then the compatible brands, because generic HEIF writers
    // put 'mif1' in front and name the codec only among the compatible brands.
    private static ImageFormat DetectIsoMedia(ReadOnlySpan<byte> data)
    {
        // A size below 12 (0 means "to the end of the file", 1 a 64-bit size) leaves only the major brand to go on;
        // the brand list is capped so that a forged size cannot make detection walk a whole file.
        long boxSize = BinaryPrimitives.ReadUInt32BigEndian(data);
        int end = (int)Math.Min(Math.Min(Math.Max(boxSize, 12), MaxBrandBytes), data.Length);

        ImageFormat found = ImageFormat.Unknown;
        for (int offset = 8; offset + 4 <= end; offset += offset == 8 ? 8 : 4)
        {
            ImageFormat brand = Brand(data.Slice(offset, 4));
            if (brand == ImageFormat.Avif)
                return ImageFormat.Avif;

            if (found == ImageFormat.Unknown)
                found = brand;
        }

        return found;
    }

    private static ImageFormat Brand(ReadOnlySpan<byte> brand)
    {
        if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8))
            return ImageFormat.Avif;

        if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) || brand.SequenceEqual("hevc"u8) ||
            brand.SequenceEqual("hevx"u8) || brand.SequenceEqual("heim"u8) || brand.SequenceEqual("heis"u8) ||
            brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8))
            return ImageFormat.Heic;

        return ImageFormat.Unknown;
    }
}
