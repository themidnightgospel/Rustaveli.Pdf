using SharpFuzz;

namespace Rustaveli.Pdf.Fuzzing;

/// <summary>
/// The fuzz targets by name. Each hands an input to a parser and lets through only the exception that parser
/// documents for input it cannot read: any other exception, a hang or memory beyond the heap limit is a finding.
/// </summary>
internal static class Targets
{
    public static IReadOnlyList<string> Names { get; } = ["fonts", "images", "svg", "pdf"];

    public static ReadOnlySpanAction Named(string name) => name switch
    {
        "fonts" => FontTarget.Run,
        "images" => ImageTarget.Run,
        "svg" => SvgTarget.Run,
        "pdf" => PdfTarget.Run,
        _ => throw new ArgumentException($"There is no fuzz target named {name}; there are {string.Join(", ", Names)}.", nameof(name)),
    };
}
