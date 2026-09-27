using System.IO.Compression;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Reading;

/// <summary>
/// Undoes the filters a stream was encoded with (ISO 32000-1, 7.4) — the general-purpose ones an operation may need
/// to read through: Flate and LZW with their predictors, the ASCII encodings and run lengths.
/// </summary>
/// <remarks>
/// Image codecs — DCT, JPX, CCITT, JBIG2 — are never decoded: pages keep their images exactly as they were encoded.
/// </remarks>
internal static class StreamDecoder
{
    private static readonly PdfName Filter = PdfNames.Filter;
    private static readonly PdfName DecodeParms = PdfNames.DecodeParms;
    private static readonly PdfName Predictor = new PdfName("Predictor");
    private static readonly PdfName Colors = new PdfName("Colors");
    private static readonly PdfName BitsPerComponent = new PdfName("BitsPerComponent");
    private static readonly PdfName Columns = new PdfName("Columns");
    private static readonly PdfName EarlyChange = new PdfName("EarlyChange");
    private static readonly PdfName Name = new PdfName("Name");

    /// <summary>
    /// The stream's data with every filter it names undone, <paramref name="resolve"/> following references in its
    /// dictionary.
    /// </summary>
    public static byte[] Decode(PdfDictionary dictionary, byte[] data, Func<PdfValue, PdfValue> resolve)
    {
        List<PdfName> filters = [];
        List<PdfDictionary?> parameters = [];

        if (dictionary.TryGetValue(Filter, out PdfValue named))
        {
            named = resolve(named);

            if (named.Kind == PdfValueKind.Name)
            {
                filters.Add(named.AsName());
            }
            else if (named.Kind == PdfValueKind.Array)
            {
                foreach (PdfValue item in named.AsArray())
                    filters.Add(resolve(item).AsName());
            }
        }

        if (dictionary.TryGetValue(DecodeParms, out PdfValue given))
        {
            given = resolve(given);

            if (given.Kind == PdfValueKind.Dictionary)
            {
                parameters.Add(given.AsDictionary());
            }
            else if (given.Kind == PdfValueKind.Array)
            {
                foreach (PdfValue item in given.AsArray())
                {
                    PdfValue resolved = resolve(item);
                    parameters.Add(resolved.Kind == PdfValueKind.Dictionary ? resolved.AsDictionary() : null);
                }
            }
        }

        for (int index = 0; index < filters.Count; index++)
            data = Apply(filters[index], data, index < parameters.Count ? parameters[index] : null, resolve);

        return data;
    }

    private static byte[] Apply(PdfName filter, byte[] data, PdfDictionary? parameters, Func<PdfValue, PdfValue> resolve) =>
        filter.Value switch
        {
            "FlateDecode" or "Fl" => Predict(Inflate(data), parameters, resolve),
            "LZWDecode" or "LZW" => Predict(Lzw(data, Integer(parameters, EarlyChange, 1, resolve) != 0), parameters, resolve),
            "ASCIIHexDecode" or "AHx" => AsciiHex(data),
            "ASCII85Decode" or "A85" => Ascii85(data),
            "RunLengthDecode" or "RL" => RunLength(data),
            "Crypt" when parameters is null || !parameters.TryGetValue(Name, out PdfValue name) || resolve(name).AsName().Value == "Identity" => data,
            _ => throw new NotSupportedException($"Streams encoded with {filter.Value} are copied as they are, never decoded."),
        };

    /// <summary>A zlib stream's data. A stream cut short gives what was there, as viewers show what they can.</summary>
    public static byte[] Inflate(byte[] data)
    {
        // The two-byte zlib header, when present, precedes the raw deflate data; the checksum after it is not checked.
        int start = data.Length >= 2 && (data[0] & 0x0F) == 8 && ((data[0] << 8) | data[1]) % 31 == 0 ? 2 : 0;

        using MemoryStream input = new MemoryStream(data, start, data.Length - start);
        using DeflateStream inflater = new DeflateStream(input, CompressionMode.Decompress);
        using MemoryStream output = new MemoryStream();
        byte[] buffer = new byte[8192];

        try
        {
            int read;
            while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, read);
        }
        catch (InvalidDataException)
        {
            // Damaged at the end: keep what inflated before the damage.
        }

        return output.ToArray();
    }

    /// <summary>Undoes a PNG or TIFF predictor, row by row (7.4.4.4).</summary>
    private static byte[] Predict(byte[] data, PdfDictionary? parameters, Func<PdfValue, PdfValue> resolve)
    {
        int predictor = Integer(parameters, Predictor, 1, resolve);

        if (predictor <= 1)
            return data;

        int colors = Math.Max(1, Integer(parameters, Colors, 1, resolve));
        int bits = Math.Max(1, Integer(parameters, BitsPerComponent, 8, resolve));
        int columns = Math.Max(1, Integer(parameters, Columns, 1, resolve));
        int pixel = Math.Max(1, ((colors * bits) + 7) / 8);
        int row = ((colors * bits * columns) + 7) / 8;

        return predictor == 2 ? Tiff(data, row, pixel, bits) : Png(data, row, pixel);
    }

    private static byte[] Png(byte[] data, int row, int pixel)
    {
        int rows = data.Length / (row + 1);
        byte[] output = new byte[rows * row];
        byte[] previous = new byte[row];

        for (int index = 0; index < rows; index++)
        {
            int source = index * (row + 1);
            byte type = data[source];
            Span<byte> current = output.AsSpan(index * row, row);
            data.AsSpan(source + 1, row).CopyTo(current);

            for (int column = 0; column < row; column++)
            {
                int left = column >= pixel ? current[column - pixel] : 0;
                int up = previous[column];
                int upLeft = column >= pixel ? previous[column - pixel] : 0;

                current[column] = (byte)(current[column] + type switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                });
            }

            current.CopyTo(previous);
        }

        return output;
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        int estimate = left + up - upLeft;
        int toLeft = Math.Abs(estimate - left);
        int toUp = Math.Abs(estimate - up);
        int toUpLeft = Math.Abs(estimate - upLeft);

        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }

    /// <summary>TIFF predictor 2, for 8-bit components: each byte is its difference from the one a pixel before.</summary>
    private static byte[] Tiff(byte[] data, int row, int pixel, int bits)
    {
        if (bits != 8)
            throw new NotSupportedException($"The TIFF predictor is undone only for 8-bit components, not {bits}-bit.");

        byte[] output = (byte[])data.Clone();

        for (int start = 0; start + row <= output.Length; start += row)
        {
            for (int column = pixel; column < row; column++)
                output[start + column] = (byte)(output[start + column] + output[start + column - pixel]);
        }

        return output;
    }

    private static byte[] Lzw(byte[] data, bool earlyChange)
    {
        List<byte[]> table = [];
        using MemoryStream output = new MemoryStream();
        int width = 9;
        int buffer = 0;
        int held = 0;
        byte[]? previous = null;

        void Reset()
        {
            table.Clear();
            for (int code = 0; code < 256; code++)
                table.Add([(byte)code]);

            table.Add([]);
            table.Add([]);
            width = 9;
            previous = null;
        }

        Reset();

        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            held += 8;

            while (held >= width)
            {
                int code = (buffer >> (held - width)) & ((1 << width) - 1);
                held -= width;

                if (code == 256)
                {
                    Reset();
                    continue;
                }

                if (code == 257)
                    return output.ToArray();

                byte[] entry;

                if (code < table.Count)
                    entry = table[code];
                else if (previous is not null)
                    entry = [.. previous, previous[0]];
                else
                    return output.ToArray();

                output.Write(entry, 0, entry.Length);

                if (previous is not null)
                    table.Add([.. previous, entry[0]]);

                previous = entry;
                int limit = table.Count + (earlyChange ? 1 : 0);

                if (limit >= (1 << width) && width < 12)
                    width++;
            }
        }

        return output.ToArray();
    }

    private static byte[] AsciiHex(byte[] data)
    {
        List<byte> output = new List<byte>(data.Length / 2);
        int high = -1;

        foreach (byte value in data)
        {
            if (value == '>')
                break;

            int digit = value switch
            {
                >= (byte)'0' and <= (byte)'9' => value - '0',
                >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
                >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
                _ => -1,
            };

            if (digit < 0)
                continue;

            if (high < 0)
            {
                high = digit;
            }
            else
            {
                output.Add((byte)((high << 4) | digit));
                high = -1;
            }
        }

        if (high >= 0)
            output.Add((byte)(high << 4));

        return output.ToArray();
    }

    private static byte[] Ascii85(byte[] data)
    {
        List<byte> output = new List<byte>(data.Length);
        uint group = 0;
        int count = 0;

        for (int index = 0; index < data.Length; index++)
        {
            byte value = data[index];

            if (value == '~')
                break;

            if (PdfParser.IsWhitespace(value))
                continue;

            if (value == 'z' && count == 0)
            {
                output.AddRange([0, 0, 0, 0]);
                continue;
            }

            if (value is < (byte)'!' or > (byte)'u')
                continue;

            group = (group * 85) + (uint)(value - '!');

            if (++count == 5)
            {
                output.AddRange([(byte)(group >> 24), (byte)(group >> 16), (byte)(group >> 8), (byte)group]);
                group = 0;
                count = 0;
            }
        }

        if (count > 1)
        {
            // A short final group is padded with the highest digit, and gives one byte fewer than it has digits.
            for (int pad = count; pad < 5; pad++)
                group = (group * 85) + 84;

            for (int index = 0; index < count - 1; index++)
                output.Add((byte)(group >> (24 - (8 * index))));
        }

        return output.ToArray();
    }

    private static byte[] RunLength(byte[] data)
    {
        List<byte> output = new List<byte>(data.Length * 2);
        int index = 0;

        while (index < data.Length)
        {
            int length = data[index++];

            if (length == 128)
                break;

            if (length < 128)
            {
                int count = Math.Min(length + 1, data.Length - index);
                output.AddRange(new ArraySegment<byte>(data, index, count));
                index += count;
            }
            else if (index < data.Length)
            {
                output.AddRange(Enumerable.Repeat(data[index++], 257 - length));
            }
        }

        return output.ToArray();
    }

    private static int Integer(PdfDictionary? parameters, PdfName key, int fallback, Func<PdfValue, PdfValue> resolve) =>
        parameters is not null && parameters.TryGetValue(key, out PdfValue value) && resolve(value) is { Kind: PdfValueKind.Integer } integer
            ? (int)integer.AsInteger()
            : fallback;
}
