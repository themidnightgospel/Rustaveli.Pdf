namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Pair adjustment format 2: glyphs grouped into classes on each side, with one value per pair of classes. This is
/// how most fonts store the bulk of their kerning.
/// </summary>
internal sealed class ClassPairAdjustment : PairAdjustment
{
    private const int RecordsOffset = 16;

    private readonly ClassDefinition _firstClasses;
    private readonly ClassDefinition _secondClasses;
    private readonly int _firstClassCount;
    private readonly int _secondClassCount;
    private readonly int _recordSize;

    public ClassPairAdjustment(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        _firstClasses = new ClassDefinition(table, offset + BigEndian.UInt16(span, offset + 8));
        _secondClasses = new ClassDefinition(table, offset + BigEndian.UInt16(span, offset + 10));
        _firstClassCount = BigEndian.UInt16(span, offset + 12);
        _secondClassCount = BigEndian.UInt16(span, offset + 14);
        _recordSize = FirstValueSize + SecondValueSize;

        // The whole class matrix must be present, so no lookup can index past it.
        long matrixSize = (long)_firstClassCount * _secondClassCount * _recordSize;
        _ = BigEndian.Slice(span, offset + (long)RecordsOffset, matrixSize);
    }

    public override bool TryGetAdjustment(ushort left, ushort right, out int adjustment)
    {
        adjustment = 0;

        if (Coverage.IndexOf(left) < 0)
            return false;

        int first = _firstClasses.ClassOf(left);
        int second = _secondClasses.ClassOf(right);

        if (first >= _firstClassCount || second >= _secondClassCount)
            return false;

        int record = Offset + RecordsOffset + (((first * _secondClassCount) + second) * _recordSize);
        adjustment = ReadXAdvance(record);
        return true;
    }
}
