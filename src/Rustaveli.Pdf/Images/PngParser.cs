using System.Buffers.Binary;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Walks a PNG's chunks, verifying each one's CRC, and collects what embedding needs. Decompresses nothing but an
/// iCCP profile.
/// </summary>
/// <remarks>
/// Critical chunks (IHDR, PLTE, IDAT, IEND) are held to the specification, and any violation rejects the file.
/// Ancillary chunks that are malformed are ignored, as libpng and browsers ignore them: they describe the image
/// rather than make it up, and a damaged gamma value is no reason to refuse pixels that are intact.
/// </remarks>
internal static class PngParser
{
    private const int SignatureLength = 8;
    private const int MaxIccProfileBytes = 16 << 20;

    public static PngFile Parse(ReadOnlyMemory<byte> source)
    {
        ReadOnlySpan<byte> data = source.Span;
        if (ImageFormatDetector.Detect(data) != ImageFormat.Png)
            throw new ImageFormatException("The data does not start with the PNG signature.");

        PngHeader? header = null;
        ReadOnlyMemory<byte> palette = default;
        ReadOnlyMemory<byte> transparency = default;
        bool hasTransparency = false;
        List<PngSegment> imageData = new List<PngSegment>();
        bool seenImageData = false;
        bool imageDataEnded = false;
        ExifOrientation? orientation = null;
        ReadOnlyMemory<byte>? iccChunk = null;
        double? gamma = null;
        Chromaticities? chromaticities = null;
        RenderingIntent? intent = null;

        int position = SignatureLength;
        while (true)
        {
            if (data.Length - position < 12)
                throw new ImageFormatException("The PNG ends before its IEND chunk.");

            uint length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(position, 4));
            ReadOnlySpan<byte> type = data.Slice(position + 4, 4);
            if (length > int.MaxValue || length > (uint)(data.Length - position - 12))
                throw new ImageFormatException($"The PNG chunk at offset {position} runs past the end of the file.");

            if (!IsChunkType(type))
                throw new ImageFormatException($"The PNG has an invalid chunk type at offset {position}.");

            int bodyOffset = position + 8;
            ReadOnlySpan<byte> body = data.Slice(bodyOffset, (int)length);
            uint storedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(bodyOffset + (int)length, 4));
            if (Crc32.Append(Crc32.Compute(type), body) != storedCrc)
                throw new ImageFormatException($"The PNG {Name(type)} chunk at offset {position} fails its CRC check.");

            position = bodyOffset + (int)length + 4;

            if (header == null && !type.SequenceEqual("IHDR"u8))
                throw new ImageFormatException("The PNG does not start with an IHDR chunk.");

            bool isImageData = type.SequenceEqual("IDAT"u8);
            if (seenImageData && !isImageData)
                imageDataEnded = true;

            if (isImageData)
            {
                if (imageDataEnded)
                    throw new ImageFormatException("The PNG's IDAT chunks are not consecutive.");

                seenImageData = true;
                if (length > 0)
                    imageData.Add(new PngSegment(bodyOffset, (int)length));
            }
            else if (type.SequenceEqual("IHDR"u8))
            {
                if (header != null)
                    throw new ImageFormatException("The PNG has more than one IHDR chunk.");
                header = ReadHeader(body);
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                if (!palette.IsEmpty)
                    throw new ImageFormatException("The PNG has more than one PLTE chunk.");
                if (seenImageData)
                    throw new ImageFormatException("The PNG's PLTE chunk follows its image data.");
                palette = ReadPalette(source.Slice(bodyOffset, (int)length), header!);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                if (!hasTransparency)
                    transparency = source.Slice(bodyOffset, (int)length);
                hasTransparency = true;
            }
            else if (type.SequenceEqual("gAMA"u8))
            {
                gamma ??= ReadGamma(body);
            }
            else if (type.SequenceEqual("cHRM"u8))
            {
                chromaticities ??= ReadChromaticities(body);
            }
            else if (type.SequenceEqual("sRGB"u8))
            {
                intent ??= body.Length == 1 && body[0] <= 3 ? (RenderingIntent)body[0] : null;
            }
            else if (type.SequenceEqual("iCCP"u8))
            {
                iccChunk ??= source.Slice(bodyOffset, (int)length);
            }
            else if (type.SequenceEqual("eXIf"u8))
            {
                orientation ??= ExifReader.ReadOrientation(body);
            }
            else if ((type[0] & 0x20) == 0)
            {
                // Bit 5 of the first letter clear (upper case) marks a chunk critical: a decoder that does not
                // understand one cannot display the image correctly and must not try.
                throw new ImageFormatException($"The PNG has an unknown critical chunk, {Name(type)}.");
            }
        }

        if (imageData.Count == 0)
            throw new ImageFormatException("The PNG has no image data.");

        PngHeader png = header!;
        if (png.ColorType == PngColorType.Palette && palette.IsEmpty)
            throw new ImageFormatException("The PNG is a palette image without a PLTE chunk.");

        ReadOnlyMemory<byte> paletteAlpha = default;
        IReadOnlyList<int>? transparentColor = null;
        if (hasTransparency)
        {
            if (png.ColorType == PngColorType.Palette)
                paletteAlpha = ReadPaletteAlpha(transparency, palette.Length / 3);
            else
                transparentColor = ReadTransparentColor(transparency.Span, png);
        }

        IccProfile? profile = iccChunk == null ? null : ReadIccProfile(iccChunk.Value, png);
        ImageMetadata metadata = new ImageMetadata(
            orientation ?? ExifOrientation.Normal,
            profile,
            gamma,
            chromaticities,
            intent);

        return new PngFile(source, png, imageData, png.ColorType == PngColorType.Palette ? palette : default,
            paletteAlpha, transparentColor, metadata);
    }

    private static PngHeader ReadHeader(ReadOnlySpan<byte> body)
    {
        if (body.Length != 13)
            throw new ImageFormatException($"The PNG IHDR chunk is {body.Length} bytes long instead of 13.");

        uint width = BinaryPrimitives.ReadUInt32BigEndian(body);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(4));
        int bitDepth = body[8];
        int colorType = body[9];

        if (width > int.MaxValue || height > int.MaxValue)
            throw new ImageFormatException($"The PNG dimensions {width} × {height} are out of range.");

        ImageLimits.CheckDimensions(width, height, "PNG");

        bool valid = colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            2 or 4 or 6 => bitDepth is 8 or 16,
            _ => false,
        };

        if (!valid)
        {
            throw new ImageFormatException(
                $"The PNG combines colour type {colorType} with bit depth {bitDepth}, which is not allowed.");
        }

        if (body[10] != 0)
            throw new ImageFormatException($"The PNG uses compression method {body[10]}; only 0 (deflate) exists.");

        if (body[11] != 0)
            throw new ImageFormatException($"The PNG uses filter method {body[11]}; only 0 exists.");

        if (body[12] > 1)
            throw new ImageFormatException($"The PNG uses interlace method {body[12]}; only 0 and 1 (Adam7) exist.");

        return new PngHeader((int)width, (int)height, bitDepth, (PngColorType)colorType, body[12] == 1);
    }

    private static ReadOnlyMemory<byte> ReadPalette(ReadOnlyMemory<byte> body, PngHeader header)
    {
        if (header.ColorType is PngColorType.Gray or PngColorType.GrayAlpha)
            throw new ImageFormatException("The PNG is a grayscale image with a PLTE chunk.");

        if (body.Length == 0 || body.Length % 3 != 0 || body.Length > 256 * 3)
        {
            throw new ImageFormatException(
                $"The PNG PLTE chunk is {body.Length} bytes long, which is not 1 to 256 entries.");
        }

        return body;
    }

    // Palette alpha longer than the palette is invalid; libpng keeps the entries that have a colour, and so does
    // this. An empty chunk says nothing and is ignored.
    private static ReadOnlyMemory<byte> ReadPaletteAlpha(ReadOnlyMemory<byte> body, int entries) =>
        body.Length > entries ? body.Slice(0, entries) : body;

    // A key sample the bit depth cannot represent can match no pixel, so it is as good as no key at all.
    private static IReadOnlyList<int>? ReadTransparentColor(ReadOnlySpan<byte> body, PngHeader header)
    {
        int samples = header.ColorType == PngColorType.Gray ? 1 : header.ColorType == PngColorType.Rgb ? 3 : 0;
        if (samples == 0 || body.Length != samples * 2)
            return null;

        int[] key = new int[samples];
        for (int index = 0; index < samples; index++)
        {
            key[index] = BinaryPrimitives.ReadUInt16BigEndian(body.Slice(index * 2, 2));
            if (key[index] >= 1 << header.BitDepth)
                return null;
        }

        return key;
    }

    private static double? ReadGamma(ReadOnlySpan<byte> body)
    {
        if (body.Length != 4)
            return null;

        uint value = BinaryPrimitives.ReadUInt32BigEndian(body);
        return value == 0 ? null : value / 100000.0;
    }

    private static Chromaticities? ReadChromaticities(ReadOnlySpan<byte> body)
    {
        if (body.Length != 32)
            return null;

        return new Chromaticities(
            Fixed(body, 0), Fixed(body, 1), Fixed(body, 2), Fixed(body, 3),
            Fixed(body, 4), Fixed(body, 5), Fixed(body, 6), Fixed(body, 7));
    }

    // PNG stores gamma and chromaticities as unsigned integers scaled by 100000.
    private static double Fixed(ReadOnlySpan<byte> body, int index) =>
        BinaryPrimitives.ReadUInt32BigEndian(body.Slice(index * 4, 4)) / 100000.0;

    // iCCP: a Latin-1 profile name of 1 to 79 bytes, a zero terminator, compression method 0, then the profile as a
    // zlib stream. A grayscale PNG must carry a GRAY profile and every other PNG an RGB one.
    private static IccProfile? ReadIccProfile(ReadOnlyMemory<byte> body, PngHeader header)
    {
        int terminator = body.Span.IndexOf((byte)0);
        if (terminator < 1 || terminator > 79 || terminator + 2 > body.Length || body.Span[terminator + 1] != 0)
            return null;

        byte[] profile;
        try
        {
            profile = ZlibReader.InflateAll(body.Slice(terminator + 2), MaxIccProfileBytes);
        }
        catch (ImageFormatException)
        {
            return null;
        }

        int components = header.ColorType is PngColorType.Gray or PngColorType.GrayAlpha ? 1 : 3;
        return IccProfile.TryCreate(profile, components);
    }

    private static bool IsChunkType(ReadOnlySpan<byte> type)
    {
        foreach (byte letter in type)
        {
            if (letter is not ((>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z')))
                return false;
        }

        return true;
    }

    private static string Name(ReadOnlySpan<byte> type) =>
        new string(new[] { (char)type[0], (char)type[1], (char)type[2], (char)type[3] });
}
