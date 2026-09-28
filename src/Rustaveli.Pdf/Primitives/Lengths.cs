namespace Rustaveli.Pdf;

public static class Lengths
{
    // A PDF point is 1/72 inch, which fixes every other factor below.
    private const float PointsPerInch = 72f;
    private const float MillimetresPerInch = 25.4f;
    private const float PointsPerPica = 12f;

    public static float ToPoints(this float value, LengthUnit unit) => unit switch
    {
        LengthUnit.Point => value,
        LengthUnit.Millimetre => value / MillimetresPerInch * PointsPerInch,
        LengthUnit.Centimetre => value * 10 / MillimetresPerInch * PointsPerInch,
        LengthUnit.Metre => value * 1000 / MillimetresPerInch * PointsPerInch,
        LengthUnit.Inch => value * PointsPerInch,
        LengthUnit.Foot => value * 12 * PointsPerInch,
        LengthUnit.Mil => value / 1000 * PointsPerInch,
        LengthUnit.Pica => value * PointsPerPica,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported unit.")
    };

    public static float Points(this float value) => value;

    public static float Millimetres(this float value) => value.ToPoints(LengthUnit.Millimetre);

    public static float Centimetres(this float value) => value.ToPoints(LengthUnit.Centimetre);

    public static float Inches(this float value) => value.ToPoints(LengthUnit.Inch);

    public static float Metres(this float value) => value.ToPoints(LengthUnit.Metre);

    public static float Feet(this float value) => value.ToPoints(LengthUnit.Foot);

    public static float Mils(this float value) => value.ToPoints(LengthUnit.Mil);

    public static float Picas(this float value) => value.ToPoints(LengthUnit.Pica);

    public static float Points(this int value) => value;

    public static float Millimetres(this int value) => ((float)value).ToPoints(LengthUnit.Millimetre);

    public static float Centimetres(this int value) => ((float)value).ToPoints(LengthUnit.Centimetre);

    public static float Inches(this int value) => ((float)value).ToPoints(LengthUnit.Inch);

    public static float Metres(this int value) => ((float)value).ToPoints(LengthUnit.Metre);

    public static float Feet(this int value) => ((float)value).ToPoints(LengthUnit.Foot);

    public static float Mils(this int value) => ((float)value).ToPoints(LengthUnit.Mil);

    public static float Picas(this int value) => ((float)value).ToPoints(LengthUnit.Pica);
}
