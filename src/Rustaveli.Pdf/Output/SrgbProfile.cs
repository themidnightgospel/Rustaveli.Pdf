using System.Text;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// An ICC version 2 display profile of sRGB, made here rather than shipped: its white point, the colorants of its
/// primaries adapted to D50, and the sRGB transfer curve as a table of 1024 points.
/// </summary>
/// <remarks>
/// PDF/A names the colour its device colours mean through an output intent, which carries a profile. The profile
/// describes exactly the colours this library writes, since every ink is written as sRGB in PDF/A.
/// </remarks>
internal static class SrgbProfile
{
    private const int CurvePoints = 1024;

    private static readonly Lazy<byte[]> Profile = new Lazy<byte[]>(Make);

    public static byte[] Bytes => Profile.Value;

    private static byte[] Make()
    {
        byte[] curve = Curve();
        List<(string Signature, byte[] Data)> tags =
        [
            ("desc", Description("sRGB IEC61966-2.1")),
            ("cprt", Text("No copyright, use freely")),
            ("wtpt", Xyz(0.9505, 1.0, 1.089)),
            ("rXYZ", Xyz(0.4360747, 0.2225045, 0.0139322)),
            ("gXYZ", Xyz(0.3850649, 0.7168786, 0.0971045)),
            ("bXYZ", Xyz(0.1430804, 0.0606169, 0.7141733)),
            ("rTRC", curve),
            ("gTRC", curve),
            ("bTRC", curve),
        ];

        using MemoryStream body = new MemoryStream();
        int tableSize = 4 + (12 * tags.Count);
        int offset = 128 + tableSize;
        List<(string Signature, int Offset, int Size)> table = [];
        List<(byte[] Data, int At)> written = [];

        foreach ((string signature, byte[] data) in tags)
        {
            // The three curves are one table, which the tag table points at three times.
            int at = written.FirstOrDefault(entry => ReferenceEquals(entry.Data, data)).At;

            if (at == 0)
            {
                at = offset + (int)body.Length;
                written.Add((data, at));
                body.Write(data, 0, data.Length);

                while (body.Length % 4 != 0)
                    body.WriteByte(0);
            }

            table.Add((signature, at, data.Length));
        }

        int size = offset + (int)body.Length;
        using MemoryStream profile = new MemoryStream(size);

        // The header: size, preferred CMM, version 2.1, a monitor profile of RGB to XYZ, dated, for Microsoft and
        // IEC's white, relative colorimetric, with D50 as the connection space's illuminant.
        UInt32(profile, (uint)size);
        Ascii(profile, "none");
        UInt32(profile, 0x02100000);
        Ascii(profile, "mntr");
        Ascii(profile, "RGB ");
        Ascii(profile, "XYZ ");
        foreach (ushort part in new ushort[] { 2026, 1, 1, 0, 0, 0 })
            UInt16(profile, part);

        Ascii(profile, "acsp");
        Ascii(profile, "MSFT");
        UInt32(profile, 0);
        Ascii(profile, "IEC ");
        Ascii(profile, "sRGB");
        profile.Write(new byte[8], 0, 8);
        UInt32(profile, 0);
        profile.Write(XyzNumber(0.9642, 1.0, 0.8249), 0, 12);
        Ascii(profile, "none");
        profile.Write(new byte[44], 0, 44);

        UInt32(profile, (uint)table.Count);
        foreach ((string signature, int at, int length) in table)
        {
            Ascii(profile, signature);
            UInt32(profile, (uint)at);
            UInt32(profile, (uint)length);
        }

        body.Position = 0;
        body.CopyTo(profile);
        return profile.ToArray();
    }

    /// <summary>The sRGB transfer curve: linear near black, a 2.4 power above, as 16-bit samples.</summary>
    private static byte[] Curve()
    {
        using MemoryStream curve = new MemoryStream();
        Ascii(curve, "curv");
        UInt32(curve, 0);
        UInt32(curve, CurvePoints);

        for (int index = 0; index < CurvePoints; index++)
        {
            double encoded = (double)index / (CurvePoints - 1);
            double linear = encoded <= 0.04045 ? encoded / 12.92 : Math.Pow((encoded + 0.055) / 1.055, 2.4);
            UInt16(curve, (ushort)Math.Round(linear * 65535));
        }

        return curve.ToArray();
    }

    private static byte[] Xyz(double x, double y, double z)
    {
        using MemoryStream tag = new MemoryStream();
        Ascii(tag, "XYZ ");
        UInt32(tag, 0);
        tag.Write(XyzNumber(x, y, z), 0, 12);
        return tag.ToArray();
    }

    private static byte[] XyzNumber(double x, double y, double z)
    {
        using MemoryStream number = new MemoryStream();
        foreach (double value in new[] { x, y, z })
            UInt32(number, (uint)(int)Math.Round(value * 65536));

        return number.ToArray();
    }

    private static byte[] Text(string text)
    {
        using MemoryStream tag = new MemoryStream();
        Ascii(tag, "text");
        UInt32(tag, 0);
        Ascii(tag, text);
        tag.WriteByte(0);
        return tag.ToArray();
    }

    /// <summary>A version 2 description: its ASCII form, with empty Unicode and Macintosh forms.</summary>
    private static byte[] Description(string text)
    {
        using MemoryStream tag = new MemoryStream();
        Ascii(tag, "desc");
        UInt32(tag, 0);
        UInt32(tag, (uint)text.Length + 1);
        Ascii(tag, text);
        tag.WriteByte(0);
        UInt32(tag, 0);
        UInt32(tag, 0);
        UInt16(tag, 0);
        tag.WriteByte(0);
        tag.Write(new byte[67], 0, 67);
        return tag.ToArray();
    }

    private static void Ascii(Stream stream, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void UInt32(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void UInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}
