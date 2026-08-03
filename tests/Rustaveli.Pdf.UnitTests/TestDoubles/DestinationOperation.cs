using System.Numerics;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

public sealed record DestinationOperation(Position Position, string Name) : DrawOperation(Position);
