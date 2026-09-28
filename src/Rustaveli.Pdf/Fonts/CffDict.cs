using System.Globalization;
using System.Text;

namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// A CFF DICT read as entries: each operator with its operands and where the entry's bytes lie, so an entry can be
/// copied into a new DICT exactly as it was written.
/// </summary>
internal static class CffDict
{
    /// <summary>The operand stack depth CFF allows in a DICT.</summary>
    private const int MaximumOperands = 48;

    /// <summary>The entries of <paramref name="dict"/>, in order.</summary>
    public static List<CffDictEntry> Read(ReadOnlySpan<byte> dict)
    {
        List<CffDictEntry> entries = new List<CffDictEntry>();
        List<double> operands = new List<double>();
        int start = 0;
        int position = 0;

        while (position < dict.Length)
        {
            int b0 = dict[position++];

            if (b0 <= 21)
            {
                int op = b0 == 12 ? (12 << 8) | BigEndian.UInt8(dict, position++) : b0;
                entries.Add(new CffDictEntry(op, operands.ToArray(), start, position - start));
                operands.Clear();
                start = position;
                continue;
            }

            if (operands.Count == MaximumOperands)
                throw new FontFormatException("A CFF DICT has more operands than CFF allows.");

            switch (b0)
            {
                case 28:
                    operands.Add(BigEndian.Int16(dict, position));
                    position += 2;
                    break;
                case 29:
                    operands.Add(BigEndian.Int32(dict, position));
                    position += 4;
                    break;
                case 30:
                    operands.Add(ReadReal(dict, ref position));
                    break;
                case >= 32 and <= 246:
                    operands.Add(b0 - 139);
                    break;
                case >= 247 and <= 250:
                    operands.Add(((b0 - 247) * 256) + BigEndian.UInt8(dict, position++) + 108);
                    break;
                case >= 251 and <= 254:
                    operands.Add(-((b0 - 251) * 256) - BigEndian.UInt8(dict, position++) - 108);
                    break;
                default:
                    throw new FontFormatException($"Byte {b0} does not begin a CFF DICT operand.");
            }
        }

        return entries;
    }

    /// <summary>
    /// A real number, packed in nibbles — digits, a point, an exponent or a minus sign — up to the one that ends it.
    /// </summary>
    private static double ReadReal(ReadOnlySpan<byte> dict, ref int position)
    {
        StringBuilder text = new StringBuilder();

        while (true)
        {
            byte packed = BigEndian.UInt8(dict, position++);

            for (int half = 0; half < 2; half++)
            {
                int nibble = half == 0 ? packed >> 4 : packed & 0x0F;

                switch (nibble)
                {
                    case <= 9:
                        text.Append((char)('0' + nibble));
                        break;
                    case 0xA:
                        text.Append('.');
                        break;
                    case 0xB:
                        text.Append('E');
                        break;
                    case 0xC:
                        text.Append("E-");
                        break;
                    case 0xE:
                        text.Append('-');
                        break;
                    case 0xF:
                        return double.TryParse(text.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                            ? value
                            : throw new FontFormatException($"\"{text}\" is not a CFF real number.");
                    default:
                        throw new FontFormatException("A CFF real number has a reserved nibble.");
                }
            }
        }
    }
}
