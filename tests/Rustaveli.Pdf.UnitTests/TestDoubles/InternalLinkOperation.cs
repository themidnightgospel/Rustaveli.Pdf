using System.Numerics;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record InternalLinkOperation(Position Position, Size Size, string Destination, Bounds Bounds)
    : DrawOperation(Position);
