namespace Rustaveli.Pdf.Writing;

/// <summary>Formats dates as PDF date strings (ISO 32000-1, 7.9.4).</summary>
internal static class PdfDate
{
    private const int Length = 23;

    /// <summary>
    /// <c>D:YYYYMMDDHHmmSS+HH'mm'</c>, in the offset the value carries. The instant is written as given rather than
    /// converted to UTC or local time, so a document stamped in Tbilisi reads back as Tbilisi time.
    /// </summary>
    public static PdfString Format(DateTimeOffset value)
    {
        Span<byte> text = stackalloc byte[Length];
        text[0] = (byte)'D';
        text[1] = (byte)':';
        WriteDigits(text.Slice(2, 4), value.Year);
        WriteDigits(text.Slice(6, 2), value.Month);
        WriteDigits(text.Slice(8, 2), value.Day);
        WriteDigits(text.Slice(10, 2), value.Hour);
        WriteDigits(text.Slice(12, 2), value.Minute);
        WriteDigits(text.Slice(14, 2), value.Second);

        // DateTimeOffset only allows whole-minute offsets, so hours and minutes express it exactly.
        int offset = (int)value.Offset.TotalMinutes;
        text[16] = offset < 0 ? (byte)'-' : (byte)'+';
        offset = Math.Abs(offset);
        WriteDigits(text.Slice(17, 2), offset / 60);
        text[19] = (byte)'\'';
        WriteDigits(text.Slice(20, 2), offset % 60);
        text[22] = (byte)'\'';

        return new PdfString(text);
    }

    private static void WriteDigits(Span<byte> destination, int value)
    {
        for (int index = destination.Length - 1; index >= 0; index--)
        {
            destination[index] = (byte)('0' + (value % 10));
            value /= 10;
        }
    }
}
