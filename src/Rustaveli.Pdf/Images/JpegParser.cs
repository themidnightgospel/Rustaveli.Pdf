using System.Buffers.Binary;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Reads a JPEG's marker segments up to the first scan — enough to describe the image to a PDF reader — without
/// touching the entropy-coded data.
/// </summary>
/// <remarks>
/// DCTDecode is specified for the Huffman-coded, 8-bit baseline, extended and progressive processes. Lossless,
/// hierarchical and arithmetic-coded frames, and 12-bit samples, are valid JPEG that readers are not required to
/// decode, so they are refused as unsupported rather than embedded as something some viewers would show blank.
/// </remarks>
internal static class JpegParser
{
    private const byte StartOfImage = 0xD8;
    private const byte EndOfImage = 0xD9;
    private const byte StartOfScan = 0xDA;
    private const byte Baseline = 0xC0;
    private const byte ExtendedSequential = 0xC1;
    private const byte Progressive = 0xC2;
    private const byte App1 = 0xE1;
    private const byte App2 = 0xE2;
    private const byte App14 = 0xEE;
    private const byte App0 = 0xE0;

    private const int IccHeaderLength = 14;

    public static JpegFile Parse(ReadOnlyMemory<byte> source)
    {
        ReadOnlySpan<byte> data = source.Span;
        if (data.Length < 4 || data[0] != 0xFF || data[1] != StartOfImage)
            throw new ImageFormatException("The data does not start with a JPEG start-of-image marker.");

        Frame? frame = null;
        bool hasAdobe = false;
        bool hasJfif = false;
        ExifOrientation? orientation = null;
        List<IccChunk>? iccChunks = null;

        int position = 2;
        while (true)
        {
            byte marker = ReadMarker(data, ref position);

            // Markers without a length: restart markers and TEM. They have no business before the first scan, but
            // they carry nothing, so skipping them is harmless.
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
                continue;

            if (marker == StartOfImage)
            {
                throw new ImageFormatException(
                    $"The JPEG has a second start-of-image marker at offset {position - 2}.");
            }

            if (marker == EndOfImage)
                throw new ImageFormatException("The JPEG ends before any image data.");

            ReadOnlySpan<byte> segment = ReadSegment(data, ref position);
            switch (marker)
            {
                case Baseline:
                case ExtendedSequential:
                case Progressive:
                    if (frame != null)
                        throw new ImageFormatException("The JPEG declares more than one frame.");
                    frame = ReadFrame(segment, marker == Progressive);
                    break;

                case 0xC3:
                case 0xC5:
                case 0xC6:
                case 0xC7:
                case 0xC9:
                case 0xCA:
                case 0xCB:
                case 0xCD:
                case 0xCE:
                case 0xCF:
                    throw new UnsupportedImageFormatException(
                        ImageFormat.Jpeg,
                        $"The JPEG uses coding process SOF{marker - 0xC0} (lossless, hierarchical or " +
                        "arithmetic-coded), which PDF readers are not required to decode; only baseline, extended " +
                        "and progressive Huffman-coded JPEGs can be embedded.");

                case StartOfScan:
                    if (frame == null)
                        throw new ImageFormatException("The JPEG image data starts before its frame header.");
                    return Build(source, frame.Value, hasAdobe, hasJfif, orientation, iccChunks);

                case App0:
                    hasJfif |= segment.StartsWith("JFIF\0"u8);
                    break;

                case App1:
                    if (orientation == null && segment.StartsWith("Exif\0\0"u8))
                        orientation = ExifReader.ReadOrientation(segment.Slice(6));
                    break;

                case App2:
                    if (segment.Length > IccHeaderLength && segment.StartsWith("ICC_PROFILE\0"u8))
                    {
                        iccChunks ??= new List<IccChunk>();
                        int offset = position - segment.Length;
                        iccChunks.Add(new IccChunk(
                            segment[12], segment[13], offset + IccHeaderLength, segment.Length - IccHeaderLength));
                    }

                    break;

                case App14:
                    hasAdobe |= segment.Length >= 12 && segment.StartsWith("Adobe"u8);
                    break;
            }
        }
    }

    private static byte ReadMarker(ReadOnlySpan<byte> data, ref int position)
    {
        if (position >= data.Length)
            throw new ImageFormatException("The JPEG ends before any image data.");

        if (data[position] != 0xFF)
            throw new ImageFormatException($"Expected a JPEG marker at offset {position}.");

        // Any number of 0xFF fill bytes may precede a marker code.
        while (position < data.Length && data[position] == 0xFF)
            position++;

        if (position >= data.Length)
            throw new ImageFormatException("The JPEG ends before any image data.");

        byte marker = data[position++];
        if (marker == 0x00)
        {
            throw new ImageFormatException(
                $"Found a stuffed zero byte where a JPEG marker was expected, at offset {position - 1}.");
        }

        return marker;
    }

    private static ReadOnlySpan<byte> ReadSegment(ReadOnlySpan<byte> data, ref int position)
    {
        if (data.Length - position < 2)
            throw new ImageFormatException("The JPEG ends inside a marker segment.");

        int length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(position, 2));
        if (length < 2)
        {
            throw new ImageFormatException(
                $"The JPEG marker segment at offset {position} has an invalid length of {length}.");
        }

        if (length > data.Length - position)
            throw new ImageFormatException("The JPEG ends inside a marker segment.");

        ReadOnlySpan<byte> segment = data.Slice(position + 2, length - 2);
        position += length;
        return segment;
    }

    private static Frame ReadFrame(ReadOnlySpan<byte> segment, bool progressive)
    {
        if (segment.Length < 6)
            throw new ImageFormatException("The JPEG frame header is truncated.");

        int precision = segment[0];
        int height = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(1, 2));
        int width = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(3, 2));
        int components = segment[5];

        if (segment.Length < 6 + (components * 3))
            throw new ImageFormatException("The JPEG frame header is truncated.");

        if (precision != 8)
        {
            throw new UnsupportedImageFormatException(
                ImageFormat.Jpeg,
                $"The JPEG has {precision}-bit samples; PDF readers decode only 8-bit JPEGs.");
        }

        if (components is not (1 or 3 or 4))
        {
            throw new UnsupportedImageFormatException(
                ImageFormat.Jpeg,
                $"The JPEG has {components} colour components; only 1 (gray), 3 (colour) and 4 (CMYK) can be " +
                "embedded.");
        }

        // A height of zero defers it to a DNL marker after the first scan, which PDF readers are not required to
        // honour and which cannot be found without walking the entropy-coded data.
        if (height == 0)
        {
            throw new UnsupportedImageFormatException(
                ImageFormat.Jpeg,
                "The JPEG defines its height in a DNL marker, which is not supported.");
        }

        ImageLimits.CheckDimensions(width, height, "JPEG");

        bool rgbIdentifiers = components == 3 &&
                              segment[6] == (byte)'R' && segment[9] == (byte)'G' && segment[12] == (byte)'B';

        return new Frame(width, height, components, progressive, rgbIdentifiers);
    }

    private static JpegFile Build(
        ReadOnlyMemory<byte> source,
        Frame frame,
        bool hasAdobe,
        bool hasJfif,
        ExifOrientation? orientation,
        List<IccChunk>? iccChunks)
    {
        IccProfile? profile = iccChunks == null ? null : AssembleProfile(source, iccChunks, frame.Components);

        return new JpegFile(
            source,
            frame.Width,
            frame.Height,
            frame.Components,
            frame.Progressive,
            hasAdobe,
            isRgb: frame.RgbIdentifiers && !hasAdobe && !hasJfif,
            orientation ?? ExifOrientation.Normal,
            profile);
    }

    // A profile longer than a marker segment can hold is split across several APP2 segments, each numbered with its
    // position and the total. They are reassembled in number order; a set with gaps, duplicates or disagreeing
    // totals is incomplete, and the profile is dropped rather than embedded wrong.
    private static IccProfile? AssembleProfile(ReadOnlyMemory<byte> source, List<IccChunk> chunks, int components)
    {
        int count = chunks[0].Count;
        if (count != chunks.Count)
            return null;

        IccChunk[] ordered = new IccChunk[count];
        int length = 0;
        foreach (IccChunk chunk in chunks)
        {
            bool valid = chunk.Count == count && chunk.Sequence >= 1 && chunk.Sequence <= count;
            if (!valid || ordered[chunk.Sequence - 1].Count != 0)
                return null;

            ordered[chunk.Sequence - 1] = chunk;
            length += chunk.Length;
        }

        if (count == 1)
            return IccProfile.TryCreate(source.Slice(ordered[0].Offset, ordered[0].Length), components);

        byte[] profile = new byte[length];
        int position = 0;
        ReadOnlySpan<byte> data = source.Span;
        foreach (IccChunk chunk in ordered)
        {
            data.Slice(chunk.Offset, chunk.Length).CopyTo(profile.AsSpan(position));
            position += chunk.Length;
        }

        return IccProfile.TryCreate(profile, components);
    }

    private readonly struct Frame(int width, int height, int components, bool progressive, bool rgbIdentifiers)
    {
        public int Width { get; } = width;

        public int Height { get; } = height;

        public int Components { get; } = components;

        public bool Progressive { get; } = progressive;

        public bool RgbIdentifiers { get; } = rgbIdentifiers;
    }

    private readonly struct IccChunk(int sequence, int count, int offset, int length)
    {
        public int Sequence { get; } = sequence;

        public int Count { get; } = count;

        public int Offset { get; } = offset;

        public int Length { get; } = length;
    }
}
