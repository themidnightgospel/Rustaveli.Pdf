namespace Rustaveli.Pdf;

/// <summary>How a path is stroked: its weight, how its ends and corners are finished, and any dashes.</summary>
/// <param name="Weight">The stroke's width, in points.</param>
/// <param name="Cap">How open ends are finished.</param>
/// <param name="Join">How corners are turned.</param>
/// <param name="MiterLimit">How long a mitered point may grow, as a multiple of the weight, before it is cut square.</param>
/// <param name="Dashes">Lengths of dash and gap, alternating and starting with a dash; none for a solid stroke.</param>
/// <param name="DashOffset">How far into the dash pattern the stroke starts.</param>
public readonly record struct LineStyle(
    float Weight,
    LineCap Cap = LineCap.Butt,
    LineJoin Join = LineJoin.Miter,
    float MiterLimit = 4,
    IReadOnlyList<float>? Dashes = null,
    float DashOffset = 0);
