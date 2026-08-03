using System.Numerics;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RoundedRectangleOperation(
    Position Position, Size Size, float CornerRadius, Color Color, float StrokeWidth, Bounds Bounds)
    : DrawOperation(Position);
