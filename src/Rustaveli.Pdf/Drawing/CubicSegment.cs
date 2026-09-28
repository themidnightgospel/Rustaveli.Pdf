namespace Rustaveli.Pdf.Drawing;

/// <summary>One cubic Bézier segment of a path, continuing from wherever the path already is.</summary>
internal readonly record struct CubicSegment(Offset Control1, Offset Control2, Offset End);
