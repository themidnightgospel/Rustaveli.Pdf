using System.Globalization;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Runs the enclosed code under a fixed culture, restoring the previous one on dispose.
/// </summary>
/// <remarks>
/// Sizes and positions format their numbers with the current culture, and they surface in exception messages.
/// Asserting those messages literally needs a known decimal separator, whatever machine the suite runs on.
/// </remarks>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous;

    public CultureScope(CultureInfo culture)
    {
        _previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
    }

    public static CultureScope Invariant() => new CultureScope(CultureInfo.InvariantCulture);

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
