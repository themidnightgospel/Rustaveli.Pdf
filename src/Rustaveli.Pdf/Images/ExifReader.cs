using System.Buffers.Binary;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Reads the orientation tag from an EXIF block — the TIFF structure carried by a JPEG APP1 segment after its
/// "Exif\0\0" identifier, or by a PNG eXIf chunk directly.
/// </summary>
/// <remarks>
/// EXIF is metadata written by a long tail of cameras and editors, and a broken block is common. Viewers ignore a
/// block they cannot read and show the pixels as stored, so this does the same: any malformation yields
/// <see cref="ExifOrientation.Normal"/> instead of failing the whole image.
/// </remarks>
internal static class ExifReader
{
    private const ushort OrientationTag = 0x0112;
    private const ushort ShortType = 3;
    private const int EntrySize = 12;

    public static ExifOrientation ReadOrientation(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
            return ExifOrientation.Normal;

        bool bigEndian;
        if (tiff[0] == (byte)'M' && tiff[1] == (byte)'M')
            bigEndian = true;
        else if (tiff[0] == (byte)'I' && tiff[1] == (byte)'I')
            bigEndian = false;
        else
            return ExifOrientation.Normal;

        if (ReadUInt16(tiff, 2, bigEndian) != 42)
            return ExifOrientation.Normal;

        uint directory = ReadUInt32(tiff, 4, bigEndian);
        if (directory > (uint)(tiff.Length - 2))
            return ExifOrientation.Normal;

        int start = (int)directory + 2;
        int count = ReadUInt16(tiff, (int)directory, bigEndian);
        for (int index = 0; index < count; index++)
        {
            int entry = start + (index * EntrySize);
            if (entry + EntrySize > tiff.Length)
                break;

            if (ReadUInt16(tiff, entry, bigEndian) != OrientationTag)
                continue;

            // A SHORT value of count 1 sits left-aligned in the entry's four-byte value field.
            if (ReadUInt16(tiff, entry + 2, bigEndian) != ShortType || ReadUInt32(tiff, entry + 4, bigEndian) != 1)
                return ExifOrientation.Normal;

            ushort value = ReadUInt16(tiff, entry + 8, bigEndian);
            return value is >= 1 and <= 8 ? (ExifOrientation)value : ExifOrientation.Normal;
        }

        return ExifOrientation.Normal;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool bigEndian) =>
        bigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2))
            : BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool bigEndian) =>
        bigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
}
