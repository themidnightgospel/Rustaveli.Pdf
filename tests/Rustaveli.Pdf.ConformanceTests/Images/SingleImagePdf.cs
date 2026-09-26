using System.Globalization;
using System.Text;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.ConformanceTests.Images;

/// <summary>
/// Writes a one-page PDF showing one <see cref="EncodedImage"/> at one point per pixel, with every member of the
/// model written as the entry it stands for. Deliberately minimal and test-only: it stands in for the real writer
/// so that the image model can be checked in an independent renderer before the two are joined.
/// </summary>
internal sealed class SingleImagePdf
{
    private readonly List<byte[]> _objects = new List<byte[]>();

    private SingleImagePdf()
    {
    }

    public static byte[] Build(EncodedImage image)
    {
        SingleImagePdf pdf = new SingleImagePdf();

        // Objects 1 to 4 are fixed; the image and anything it refers to follow.
        pdf.Add("<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Add("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {image.Width} {image.Height}] " +
                "/Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>");
        pdf.AddStream(string.Empty, Encoding.ASCII.GetBytes($"q {image.Width} 0 0 {image.Height} 0 0 cm /Im0 Do Q"));
        pdf.AddImage(image);

        return pdf.Serialise();
    }

    private static string Number(double value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

    private int Add(string dictionary)
    {
        _objects.Add(Encoding.ASCII.GetBytes(dictionary));
        return _objects.Count;
    }

    private int AddStream(string entries, ReadOnlySpan<byte> data)
    {
        byte[] head = Encoding.ASCII.GetBytes($"<< {entries} /Length {data.Length} >>\nstream\n");
        byte[] tail = Encoding.ASCII.GetBytes("\nendstream");
        _objects.Add([.. head, .. data, .. tail]);
        return _objects.Count;
    }

    // Reserves an object number, so that an image can name its soft mask before the mask is written.
    private int Reserve()
    {
        _objects.Add([]);
        return _objects.Count;
    }

    private int AddImage(EncodedImage image)
    {
        int number = Reserve();
        StringBuilder entries = new StringBuilder("/Type /XObject /Subtype /Image");
        entries.Append(CultureInfo.InvariantCulture, $" /Width {image.Width} /Height {image.Height}");
        entries.Append(" /ColorSpace ").Append(ColorSpace(image.ColorSpace));
        entries.Append(CultureInfo.InvariantCulture, $" /BitsPerComponent {image.BitsPerComponent}");
        entries.Append(image.Filter == ImageFilter.Dct ? " /Filter /DCTDecode" : " /Filter /FlateDecode");

        if (image.DecodeParameters is { } parameters)
        {
            entries.Append(CultureInfo.InvariantCulture,
                $" /DecodeParms << /Predictor {parameters.Predictor} /Colors {parameters.Colors} " +
                $"/BitsPerComponent {parameters.BitsPerComponent} /Columns {parameters.Columns} >>");
        }

        if (image.ColorTransform is { } transform)
            entries.Append(CultureInfo.InvariantCulture, $" /DecodeParms << /ColorTransform {transform} >>");

        if (image.Decode != null)
            entries.Append(" /Decode [").Append(string.Join(" ", image.Decode.Select(Number))).Append(']');

        if (image.ColorKeyMask != null)
            entries.Append(" /Mask [").Append(string.Join(" ", image.ColorKeyMask)).Append(']');

        if (image.SoftMask != null)
            entries.Append(CultureInfo.InvariantCulture, $" /SMask {AddImage(image.SoftMask)} 0 R");

        byte[] head = Encoding.ASCII.GetBytes($"<< {entries} /Length {image.Data.Length} >>\nstream\n");
        _objects[number - 1] = [.. head, .. image.Data.Span, .. "\nendstream"u8];
        return number;
    }

    private string ColorSpace(ImageColorSpace space)
    {
        switch (space.Kind)
        {
            case ImageColorSpaceKind.DeviceGray:
                return "/DeviceGray";

            case ImageColorSpaceKind.DeviceRgb:
                return "/DeviceRGB";

            case ImageColorSpaceKind.DeviceCmyk:
                return "/DeviceCMYK";

            case ImageColorSpaceKind.Indexed:
                string lookup = string.Concat(space.Palette.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
                return $"[/Indexed {ColorSpace(space.Base!)} {space.HighValue} <{lookup}>]";

            default:
                IccProfile profile = space.Profile!;
                int stream = AddStream($"/N {profile.ComponentCount} /Alternate {ColorSpace(space.Alternate!)}", profile.Data.Span);
                return $"[/ICCBased {stream} 0 R]";
        }
    }

    private byte[] Serialise()
    {
        using MemoryStream output = new MemoryStream();
        output.Write("%PDF-1.7\n"u8);

        // A comment of bytes above 127 on the second line marks the file as binary, as the specification advises.
        output.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);

        List<long> offsets = new List<long>();
        for (int index = 0; index < _objects.Count; index++)
        {
            offsets.Add(output.Position);
            output.Write(Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n"));
            output.Write(_objects[index]);
            output.Write("\nendobj\n"u8);
        }

        long xref = output.Position;
        StringBuilder table = new StringBuilder();
        table.Append(CultureInfo.InvariantCulture, $"xref\n0 {_objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets)
            table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {_objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        output.Write(Encoding.ASCII.GetBytes(table.ToString()));

        return output.ToArray();
    }
}
