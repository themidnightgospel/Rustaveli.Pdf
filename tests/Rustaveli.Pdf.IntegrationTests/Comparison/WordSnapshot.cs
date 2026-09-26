namespace Rustaveli.Pdf.IntegrationTests.Comparison;

/// <summary>
/// A word extracted from a rendered PDF, with its position on the page.
/// </summary>
/// <param name="Text">The word as the reader recovered it.</param>
/// <param name="Left">Distance from the left edge, in points.</param>
/// <param name="Top">Distance from the top edge, in points, converted from PDF's bottom-left origin.</param>
public readonly record struct WordSnapshot(string Text, double Left, double Top);
