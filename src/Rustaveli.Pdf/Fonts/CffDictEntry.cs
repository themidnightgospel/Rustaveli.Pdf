namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// One entry of a CFF DICT: its operator — a byte, or 12 then a byte, as <c>(12 &lt;&lt; 8) | byte</c> — its operands,
/// and where its bytes, operands and operator together, lie within the DICT.
/// </summary>
internal sealed record CffDictEntry(int Operator, double[] Operands, int Start, int Length)
{
    /// <summary>The single operand, as an integer; a DICT written otherwise is not a font.</summary>
    public int Integer(int index = 0) =>
        index < Operands.Length && Operands[index] == Math.Floor(Operands[index]) && Math.Abs(Operands[index]) <= int.MaxValue
            ? (int)Operands[index]
            : throw new FontFormatException($"CFF DICT operator {Operator} lacks the integer operand it needs.");
}
