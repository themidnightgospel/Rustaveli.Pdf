namespace Rustaveli.Pdf;

public static class Lengths
{
    // A PDF point is 1/72 inch, which fixes every other factor below.
    private const float PointsPerInch = 72f;
    private const float MillimetresPerInch = 25.4f;

    public static float ToPoints(this float value, LengthUnit unit) => unit switch
    {
        LengthUnit.Point => value,
        LengthUnit.Millimetre => value / MillimetresPerInch * PointsPerInch,
        LengthUnit.Centimetre => value * 10 / MillimetresPerInch * PointsPerInch,
        LengthUnit.Metre => value * 1000 / MillimetresPerInch * PointsPerInch,
        LengthUnit.Inch => value * PointsPerInch,
        LengthUnit.Foot => value * 12 * PointsPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported unit.")
    };

    public static float Points(this float value) => value;

    public static float Millimetres(this float value) => value.ToPoints(LengthUnit.Millimetre);

    public static float Centimetres(this float value) => value.ToPoints(LengthUnit.Centimetre);

    public static float Inches(this float value) => value.ToPoints(LengthUnit.Inch);

    public static float Points(this int value) => value;

    public static float Millimetres(this int value) => ((float)value).ToPoints(LengthUnit.Millimetre);

    public static float Centimetres(this int value) => ((float)value).ToPoints(LengthUnit.Centimetre);

    public static float Inches(this int value) => ((float)value).ToPoints(LengthUnit.Inch);
}
