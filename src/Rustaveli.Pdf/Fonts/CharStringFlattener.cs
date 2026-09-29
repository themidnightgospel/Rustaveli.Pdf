namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Rewrites a Type 2 charstring with every subroutine it calls written out in place, so the glyph draws the same with
/// no subroutines at all — what a subset needs, since it keeps only its own glyphs and none of the subroutines shared
/// with the glyphs it drops.
/// </summary>
/// <remarks>
/// <para>
/// A call takes the subroutine's number from the top of the stack, biased by the size of the subroutine INDEX; the
/// operands beneath it stay on the stack for the subroutine to use, so they are written out ahead of its body. A
/// subroutine ends at <c>return</c>, or ends the glyph at <c>endchar</c>.
/// </para>
/// <para>
/// A hint mask is followed by a byte for every eight stem hints declared so far — declared by the stem operators, and
/// implicitly by any operands a hint mask itself finds on the stack — so the stems are counted as the glyph goes.
/// </para>
/// <para>
/// What cannot be rewritten this way is refused with <see cref="NotSupportedException"/>, for the caller to embed the
/// font whole: a subroutine number computed rather than written, arithmetic and storage operators, an accented glyph
/// composed from two others the old way, and calls nested deeper than Type 2 allows.
/// </para>
/// </remarks>
internal sealed class CharStringFlattener
{
    /// <summary>The deepest subroutine calls may nest.</summary>
    private const int MaximumDepth = 10;

    private const int HStem = 1;
    private const int VStem = 3;
    private const int CallSubr = 10;
    private const int Return = 11;
    private const int Escape = 12;
    private const int EndChar = 14;
    private const int HStemHm = 18;
    private const int HintMask = 19;
    private const int CntrMask = 20;
    private const int VStemHm = 23;
    private const int CallGSubr = 29;

    private readonly byte[] _cff;
    private readonly CffIndex _globalSubrs;
    private readonly CffIndex _localSubrs;
    private readonly FontDataWriter _output = new FontDataWriter();

    /// <summary>The operands on the stack not yet written out, each with where its bytes lie.</summary>
    private readonly List<(int Start, int Length, double Value)> _pending = [];

    /// <summary>
    /// How many operands the stack holds, written out or not: those pushed before a call or left by a return are
    /// written out, yet still count toward the stems the next operator declares.
    /// </summary>
    private int _stack;

    private int _stems;

    private CharStringFlattener(byte[] cff, CffIndex globalSubrs, CffIndex localSubrs)
    {
        _cff = cff;
        _globalSubrs = globalSubrs;
        _localSubrs = localSubrs;
    }

    /// <summary>The charstring at <paramref name="start"/>, with the subroutines it calls written out.</summary>
    /// <param name="cff">The whole CFF table the charstring and subroutines are in.</param>
    /// <param name="start">Where the charstring begins in <paramref name="cff"/>.</param>
    /// <param name="length">The charstring's length.</param>
    /// <param name="globalSubrs">The font's global subroutines.</param>
    /// <param name="localSubrs">The subroutines of the font dict the glyph belongs to.</param>
    public static byte[] Flatten(byte[] cff, int start, int length, CffIndex globalSubrs, CffIndex localSubrs)
    {
        CharStringFlattener flattener = new CharStringFlattener(cff, globalSubrs, localSubrs);

        if (!flattener.Run(start, length, 0))
            throw new FontFormatException("A charstring ends without endchar.");

        return flattener._output.ToArray();
    }

    /// <summary>The bias added to a subroutine number, which depends on how many subroutines there are.</summary>
    public static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

    /// <summary>Writes out the program at <paramref name="start"/>; true once it reaches <c>endchar</c>.</summary>
    private bool Run(int start, int length, int depth)
    {
        int end = start + length;
        int position = start;

        while (position < end)
        {
            int b0 = _cff[position];

            if (b0 is 28 or >= 32)
            {
                position = Operand(position, b0, end);
                continue;
            }

            position++;

            switch (b0)
            {
                case CallSubr:
                case CallGSubr:
                    if (Call(b0 == CallGSubr ? _globalSubrs : _localSubrs, depth))
                        return true;

                    break;

                case Return:
                    // The operands left beneath the return are the caller's to use.
                    WritePending();
                    return false;

                case EndChar:
                    // Four operands more than a width are an accented glyph composed of two others, which a subset
                    // would have to find by their standard names.
                    if (_stack >= 4)
                        throw new NotSupportedException("An accented glyph composed with endchar is not subset.");

                    WriteOperator(b0);
                    return true;

                case HStem:
                case VStem:
                case HStemHm:
                case VStemHm:
                    _stems += _stack / 2;
                    WriteOperator(b0);
                    break;

                case HintMask:
                case CntrMask:
                    // Operands before a hint mask declare vertical stems, as if a vstemhm had come first.
                    _stems += _stack / 2;
                    WriteOperator(b0);
                    int maskLength = (_stems + 7) / 8;

                    if (position + maskLength > end)
                        throw FontFormatException.Truncated();

                    _output.Bytes(_cff.AsSpan(position, maskLength));
                    position += maskLength;
                    break;

                case Escape:
                    if (position >= end)
                        throw FontFormatException.Truncated();

                    int second = _cff[position++];

                    // Only the flex operators, 34 to 37, draw, and dotsection, 0, deprecated, does nothing at all: it
                    // is kept as it is, being common in fonts converted from Type 1, where refusing it would embed the
                    // whole font. The rest compute and store values on the stack.
                    if (second is not (0 or (>= 34 and <= 37)))
                        throw new NotSupportedException($"Charstring operator 12 {second} is not subset.");

                    WriteOperator(Escape, second);
                    break;

                case 0 or 2 or 9 or 13 or 15 or 16 or 17:
                    throw new FontFormatException($"Charstring operator {b0} is reserved.");

                default:
                    WriteOperator(b0);
                    break;
            }
        }

        // A subroutine may end where its bytes end, as if with a return; a glyph may not.
        WritePending();
        return false;
    }

    /// <summary>
    /// Reads the operand at <paramref name="position"/> onto the stack; where the next token begins. An operand that
    /// runs past <paramref name="end"/>, into whatever follows the program, is refused.
    /// </summary>
    private int Operand(int position, int b0, int end)
    {
        int length = b0 switch
        {
            28 => 3,
            <= 246 => 1,
            <= 254 => 2,
            _ => 5,
        };

        if (position + length > end)
            throw FontFormatException.Truncated();

        double value = b0 switch
        {
            28 => BigEndian.Int16(_cff, position + 1),
            <= 246 => b0 - 139,
            <= 250 => ((b0 - 247) * 256) + _cff[position + 1] + 108,
            <= 254 => -((b0 - 251) * 256) - _cff[position + 1] - 108,
            _ => BigEndian.Int32(_cff, position + 1) / 65536d,
        };

        _pending.Add((position, length, value));
        _stack++;
        return position + length;
    }

    /// <summary>Calls the subroutine numbered on top of the stack; true once it ends the glyph.</summary>
    private bool Call(CffIndex subrs, int depth)
    {
        if (depth == MaximumDepth)
            throw new NotSupportedException("Subroutine calls nest deeper than Type 2 allows.");

        if (_stack == 0)
            throw new FontFormatException("A subroutine call finds no number on the stack.");

        // A number left on the stack by a subroutine's return has been written out already, and cannot be taken back.
        if (_pending.Count == 0)
            throw new NotSupportedException("A subroutine number left by another subroutine is not subset.");

        double number = _pending[^1].Value;
        _pending.RemoveAt(_pending.Count - 1);
        _stack--;

        if (number != Math.Floor(number))
            throw new NotSupportedException("A subroutine number that is not a whole number is not subset.");

        int index = (int)number + Bias(subrs.Count);

        if (index < 0 || index >= subrs.Count)
            throw new FontFormatException($"Subroutine {index} does not exist.");

        // What stays on the stack is the subroutine's to use, so it is written out before the subroutine's own bytes.
        WritePending();
        (int start, int length) = subrs.GetItem(_cff, index);
        return Run(start, length, depth + 1);
    }

    /// <summary>Writes out an operator, which consumes the whole stack: every drawing operator clears it.</summary>
    private void WriteOperator(int op, int second = -1)
    {
        WritePending();
        _output.UInt8(op);

        if (second >= 0)
            _output.UInt8(second);

        _stack = 0;
    }

    /// <summary>Writes out the operands not yet written; they stay on the stack until an operator consumes them.</summary>
    private void WritePending()
    {
        foreach ((int start, int length, _) in _pending)
            _output.Bytes(_cff.AsSpan(start, length));

        _pending.Clear();
    }
}
