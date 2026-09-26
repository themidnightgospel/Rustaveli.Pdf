using System.Text;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// Builds JPEG marker streams segment by segment. The scan data is filler: the loader never decodes it, which is
/// the point.
/// </summary>
internal static class TestJpeg
{
    public const byte Baseline = 0xC0;

    /// <summary>SOI, the given segments, then a scan and EOI.</summary>
    public static byte[] Build(params byte[][] segments) => [0xFF, 0xD8, .. segments.SelectMany(s => s), .. Scan()];

    /// <summary>SOI and the given bytes, exactly: no scan is appended.</summary>
    public static byte[] Raw(params byte[][] parts) => [0xFF, 0xD8, .. parts.SelectMany(s => s)];

    public static byte[] Segment(byte marker, byte[] payload)
    {
        int length = payload.Length + 2;
        return [0xFF, marker, (byte)(length >> 8), (byte)length, .. payload];
    }

    /// <summary>A frame header; one component per identifier.</summary>
    public static byte[] Frame(int width, int height, byte[] identifiers, byte marker = Baseline, int precision = 8)
    {
        List<byte> payload = [(byte)precision, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width,
            (byte)identifiers.Length];
        foreach (byte identifier in identifiers)
            payload.AddRange([identifier, 0x11, 0x00]);
        return Segment(marker, payload.ToArray());
    }

    public static byte[] Frame(int width, int height, int components, byte marker = Baseline, int precision = 8) =>
        Frame(width, height, Enumerable.Range(1, components).Select(id => (byte)id).ToArray(), marker, precision);

    public static byte[] Scan() => [0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x12, 0x34, 0xFF, 0x00, 0x56, 0xFF, 0xD9];

    public static byte[] Jfif() => Segment(0xE0, [.. "JFIF\0"u8, 0x01, 0x02, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

    public static byte[] Adobe(byte transform) =>
        Segment(0xEE, [.. "Adobe"u8, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, transform]);

    public static byte[] Exif(byte[] tiff) => Segment(0xE1, [.. "Exif\0\0"u8, .. tiff]);

    public static byte[] Icc(int sequence, int count, byte[] data) =>
        Segment(0xE2, [.. "ICC_PROFILE\0"u8, (byte)sequence, (byte)count, .. data]);

    /// <summary>A minimal TIFF structure holding one directory with the given entries.</summary>
    public static byte[] Tiff(bool bigEndian, params (ushort Tag, ushort Type, uint Count, uint Value)[] entries)
    {
        List<byte> data = [];
        data.AddRange(bigEndian ? "MM"u8.ToArray() : "II"u8.ToArray());
        data.AddRange(UInt16(42, bigEndian));
        data.AddRange(UInt32(8, bigEndian));
        data.AddRange(UInt16((ushort)entries.Length, bigEndian));
        foreach ((ushort tag, ushort type, uint count, uint value) in entries)
        {
            data.AddRange(UInt16(tag, bigEndian));
            data.AddRange(UInt16(type, bigEndian));
            data.AddRange(UInt32(count, bigEndian));

            // A SHORT value sits left-aligned in the four-byte field.
            if (type == 3)
                data.AddRange([.. UInt16((ushort)value, bigEndian), 0, 0]);
            else
                data.AddRange(UInt32(value, bigEndian));
        }

        data.AddRange(UInt32(0, bigEndian));
        return data.ToArray();
    }

    public static byte[] Orientation(int value, bool bigEndian = false) => Tiff(bigEndian, (0x0112, 3, 1, (uint)value));

    /// <summary>A structurally valid ICC profile header followed by filler, declaring <paramref name="size"/> bytes.</summary>
    public static byte[] Profile(string colorSpace, int size = 200, int? declaredSize = null)
    {
        byte[] profile = new byte[size];
        TestPng.WriteUInt32(profile, 0, (uint)(declaredSize ?? size));
        Encoding.ASCII.GetBytes("mntr", 0, 4, profile, 12);
        Encoding.ASCII.GetBytes(colorSpace, 0, 4, profile, 16);
        Encoding.ASCII.GetBytes("XYZ ", 0, 4, profile, 20);
        Encoding.ASCII.GetBytes("acsp", 0, 4, profile, 36);
        for (int index = 132; index < size; index++)
            profile[index] = (byte)index;
        return profile;
    }

    private static byte[] UInt16(ushort value, bool bigEndian) =>
        bigEndian ? [(byte)(value >> 8), (byte)value] : [(byte)value, (byte)(value >> 8)];

    private static byte[] UInt32(uint value, bool bigEndian) =>
        bigEndian
            ? [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]
            : [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
}
