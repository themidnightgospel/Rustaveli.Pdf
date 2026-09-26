namespace Rustaveli.Pdf.IntegrationTests.Comparison;

/// <summary>
/// The observable content of one rendered page.
/// </summary>
public sealed record PageSnapshot(double Width, double Height, IReadOnlyList<WordSnapshot> Words)
{
    public string Text => string.Join(" ", Words.Select(word => word.Text));
}
