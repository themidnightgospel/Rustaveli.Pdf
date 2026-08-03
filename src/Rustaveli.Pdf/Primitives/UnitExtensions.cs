namespace Rustaveli.Pdf.Primitives;

public static class UnitExtensions
{
    // A PDF point is 1/72 inch, which fixes every other factor below.
    private const float PointsPerInch = 72f;
    private const float MillimetresPerInch = 25.4f;

    public static float ToPoints(this float value, Unit unit) => unit switch
    {
        Unit.Point => value,
        Unit.Millimetre => value / MillimetresPerInch * PointsPerInch,
        Unit.Centimetre => value * 10 / MillimetresPerInch * PointsPerInch,
        Unit.Metre => value * 1000 / MillimetresPerInch * PointsPerInch,
        Unit.Inch => value * PointsPerInch,
        Unit.Feet => value * 12 * PointsPerInch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported unit.")
    };

    public static float Points(this float value) => value;

    public static float Millimetres(this float value) => value.ToPoints(Unit.Millimetre);

    public static float Centimetres(this float value) => value.ToPoints(Unit.Centimetre);

    public static float Inches(this float value) => value.ToPoints(Unit.Inch);

    public static float Points(this int value) => value;

    public static float Millimetres(this int value) => ((float)value).ToPoints(Unit.Millimetre);

    public static float Centimetres(this int value) => ((float)value).ToPoints(Unit.Centimetre);

    public static float Inches(this int value) => ((float)value).ToPoints(Unit.Inch);
}
