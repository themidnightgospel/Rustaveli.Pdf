namespace Rustaveli.Pdf.IntegrationTests.Comparison;

/// <summary>
/// The data a recipe renders, shared so both implementations describe genuinely identical documents.
/// </summary>
public static class RecipeData
{
    // Committed and registered with both libraries (TestFonts), so the two render with identical metrics on every
    // OS; a system font would be substituted differently by each library wherever it is missing.
    public const string FontFamily = TestFonts.Sans;
    public const float FontSize = 11f;
    public const float PageWidth = 595f;
    public const float PageHeight = 842f;
    public const float Margin = 40f;

    public const string Title = "Quarterly Statement";
    public const string Intro = "This statement summarises the transactions recorded during the period.";

    // Sized to overflow an A4 page several times over, so pagination is exercised rather than assumed.
    public static IReadOnlyList<(string Code, string Description, string Amount)> Rows { get; } =
        Enumerable.Range(1, 150)
            .Select(index => ($"SKU-{index:D3}", $"Line item number {index}", $"{index * 12.5m:F2}"))
            .ToList();

    public static IReadOnlyList<string> Paragraphs { get; } =
        Enumerable.Range(1, 60)
            .Select(index => $"Paragraph {index}. The quick brown fox jumps over the lazy dog near the river bank.")
            .ToList();
}
