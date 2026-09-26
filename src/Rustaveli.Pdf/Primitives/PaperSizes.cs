namespace Rustaveli.Pdf;

/// <summary>
/// Standard page dimensions in PDF points, portrait unless stated otherwise.
/// </summary>
public static class PaperSizes
{
    public static Extent A0 { get; } = FromMillimetres(841, 1189);
    public static Extent A1 { get; } = FromMillimetres(594, 841);
    public static Extent A2 { get; } = FromMillimetres(420, 594);
    public static Extent A3 { get; } = FromMillimetres(297, 420);
    public static Extent A4 { get; } = FromMillimetres(210, 297);
    public static Extent A5 { get; } = FromMillimetres(148, 210);
    public static Extent A6 { get; } = FromMillimetres(105, 148);

    public static Extent Letter { get; } = FromInches(8.5f, 11f);
    public static Extent Legal { get; } = FromInches(8.5f, 14f);
    public static Extent Tabloid { get; } = FromInches(11f, 17f);
    public static Extent Executive { get; } = FromInches(7.25f, 10.5f);

    private static Extent FromMillimetres(float width, float height) =>
        new(width.Millimetres(), height.Millimetres());

    private static Extent FromInches(float width, float height) =>
        new(width.Inches(), height.Inches());

    /// <summary>Swaps width and height, turning a portrait size into landscape.</summary>
    public static Extent Landscape(this Extent size) => new(size.Height, size.Width);

    /// <summary>Ensures the taller dimension is the height.</summary>
    public static Extent Portrait(this Extent size) =>
        size.Width > size.Height ? new Extent(size.Height, size.Width) : size;
}
