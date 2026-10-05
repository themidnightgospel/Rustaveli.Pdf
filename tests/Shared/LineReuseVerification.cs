using System.Runtime.CompilerServices;

namespace Rustaveli.Pdf.Testing;

/// <summary>
/// Turns on checking of every reuse of lines across widths, for every test in the suite: each is rebuilt afresh and
/// compared bit for bit, and a difference fails the test that caused it.
/// </summary>
internal static class LineReuseVerification
{
    [ModuleInitializer]
    internal static void TurnOn() => Rustaveli.Pdf.Blocks.TextBlock.VerifiesReuse = true;
}
