using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// One change to a font file: where it lands, and what it writes there.
/// </summary>
/// <remarks>
/// Uniformly random flips mostly land in glyph outlines, which the parser never interprets. Aiming most mutations at
/// the table directory and at the tables themselves — and writing the values that break parsers, such as 0, 0xFFFF
/// and 0x7FFFFFFF over counts and offsets — reaches the code that sizes and indexes things by what it reads.
/// </remarks>
/// <param name="Region">0: anywhere in the file; 1: the header and table directory; 2: inside one table.</param>
/// <param name="Selector">Chooses the table, for region 2, and the position within the region.</param>
/// <param name="Kind">0: one random byte; 1: one flipped bit; 2: a 16-bit value; 3: a 32-bit value.</param>
/// <param name="Value">The value written; for kinds 2 and 3, small values pick an edge case instead.</param>
public readonly record struct FontMutation(int Region, int Selector, int Kind, int Value)
{
    private static readonly uint[] EdgeValues =
        [0, 1, 0x7F, 0x80, 0xFF, 0x7FFF, 0x8000, 0xFFFF, 0x7FFFFFFF, 0xFFFFFFFF];

    public void ApplyTo(byte[] font, IReadOnlyList<(int Offset, int Length)> tables)
    {
        (int start, int length) = Region switch
        {
            1 => (0, Math.Min(font.Length, 12 + (16 * Math.Max(1, tables.Count)))),
            2 when tables.Count > 0 => tables[Selector % tables.Count],
            _ => (0, font.Length)
        };

        if (length <= 0)
            return;

        int position = start + (int)((uint)Selector % (uint)length);
        uint value = Value is >= 0 and < 10 ? EdgeValues[Value] : (uint)Value;

        switch (Kind)
        {
            case 0:
                font[position] = (byte)value;
                break;
            case 1:
                font[position] ^= (byte)(1 << (Value & 7));
                break;
            case 2 when position + 2 <= font.Length:
                BigEndian.WriteUInt16(font, position & ~1, (ushort)value);
                break;
            case 3 when position + 4 <= font.Length:
                BigEndian.WriteUInt32(font, position & ~1, value);
                break;
        }
    }
}
