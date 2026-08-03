using System.Numerics;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record RectangleOperation(Position Position, Size Size, Color Color, Bounds Bounds) : DrawOperation(Position);
