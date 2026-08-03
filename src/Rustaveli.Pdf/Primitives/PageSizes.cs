namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// Standard page dimensions in PDF points, portrait unless stated otherwise.
/// </summary>
public static class PageSizes
{
    public static Size A0 { get; } = FromMillimetres(841, 1189);
    public static Size A1 { get; } = FromMillimetres(594, 841);
    public static Size A2 { get; } = FromMillimetres(420, 594);
    public static Size A3 { get; } = FromMillimetres(297, 420);
    public static Size A4 { get; } = FromMillimetres(210, 297);
    public static Size A5 { get; } = FromMillimetres(148, 210);
    public static Size A6 { get; } = FromMillimetres(105, 148);

    public static Size Letter { get; } = FromInches(8.5f, 11f);
    public static Size Legal { get; } = FromInches(8.5f, 14f);
    public static Size Tabloid { get; } = FromInches(11f, 17f);
    public static Size Executive { get; } = FromInches(7.25f, 10.5f);

    private static Size FromMillimetres(float width, float height) =>
        new(width.Millimetres(), height.Millimetres());

    private static Size FromInches(float width, float height) =>
        new(width.Inches(), height.Inches());

    /// <summary>Swaps width and height, turning a portrait size into landscape.</summary>
    public static Size Landscape(this Size size) => new(size.Height, size.Width);

    /// <summary>Ensures the taller dimension is the height.</summary>
    public static Size Portrait(this Size size) =>
        size.Width > size.Height ? new Size(size.Height, size.Width) : size;
}
