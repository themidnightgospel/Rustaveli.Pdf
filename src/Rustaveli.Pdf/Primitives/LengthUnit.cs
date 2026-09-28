namespace Rustaveli.Pdf;

/// <summary>
/// Physical units convertible to PDF points, the unit every layout API expects.
/// </summary>
public enum LengthUnit
{
    Point,
    Millimetre,
    Centimetre,
    Metre,
    Inch,
    Foot,

    /// <summary>A thousandth of an inch, as circuit boards and film thicknesses are measured.</summary>
    Mil,

    /// <summary>Twelve points, the printer's unit for column widths and indents.</summary>
    Pica
}
