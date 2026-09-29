using System.Globalization;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Runs the enclosed code under a fixed culture, restoring the previous one on dispose.
/// </summary>
/// <remarks>
/// Sizes, positions and the messages built from them read the same in every culture. Tests that assert them word for
/// word run under <see cref="DecimalComma"/>, so that one formatted in the current culture would fail.
/// </remarks>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous;

    public CultureScope(CultureInfo culture)
    {
        _previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
    }

    /// <summary>A culture that writes decimals with a comma, as German does.</summary>
    public static CultureScope DecimalComma() => new CultureScope(new CultureInfo("de-DE"));

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
