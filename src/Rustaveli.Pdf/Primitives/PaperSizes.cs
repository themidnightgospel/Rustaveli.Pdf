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
    public static Extent A7 { get; } = FromMillimetres(74, 105);
    public static Extent A8 { get; } = FromMillimetres(52, 74);
    public static Extent A9 { get; } = FromMillimetres(37, 52);
    public static Extent A10 { get; } = FromMillimetres(26, 37);

    public static Extent B0 { get; } = FromMillimetres(1000, 1414);
    public static Extent B1 { get; } = FromMillimetres(707, 1000);
    public static Extent B2 { get; } = FromMillimetres(500, 707);
    public static Extent B3 { get; } = FromMillimetres(353, 500);
    public static Extent B4 { get; } = FromMillimetres(250, 353);
    public static Extent B5 { get; } = FromMillimetres(176, 250);
    public static Extent B6 { get; } = FromMillimetres(125, 176);
    public static Extent B7 { get; } = FromMillimetres(88, 125);
    public static Extent B8 { get; } = FromMillimetres(62, 88);
    public static Extent B9 { get; } = FromMillimetres(44, 62);
    public static Extent B10 { get; } = FromMillimetres(31, 44);

    /// <summary>ISO 269 C sizes: envelopes that take the A size of the same number unfolded.</summary>
    public static Extent C0 { get; } = FromMillimetres(917, 1297);
    public static Extent C1 { get; } = FromMillimetres(648, 917);
    public static Extent C2 { get; } = FromMillimetres(458, 648);
    public static Extent C3 { get; } = FromMillimetres(324, 458);
    public static Extent C4 { get; } = FromMillimetres(229, 324);
    public static Extent C5 { get; } = FromMillimetres(162, 229);
    public static Extent C6 { get; } = FromMillimetres(114, 162);
    public static Extent C7 { get; } = FromMillimetres(81, 114);
    public static Extent C8 { get; } = FromMillimetres(57, 81);
    public static Extent C9 { get; } = FromMillimetres(40, 57);
    public static Extent C10 { get; } = FromMillimetres(28, 40);

    /// <summary>The DL envelope, which takes an A4 sheet folded in three.</summary>
    public static Extent EnvelopeDL { get; } = FromMillimetres(110, 220);

    /// <summary>The North American No. 10 business envelope.</summary>
    public static Extent EnvelopeNo10 { get; } = FromInches(4.125f, 9.5f);

    /// <summary>The international postcard, A6 rounded to the nearest millimetre as the post trade cuts it.</summary>
    public static Extent Postcard { get; } = FromMillimetres(100, 148);

    public static Extent Letter { get; } = FromInches(8.5f, 11f);
    public static Extent Legal { get; } = FromInches(8.5f, 14f);
    public static Extent Tabloid { get; } = FromInches(11f, 17f);

    /// <summary>Tabloid turned on its side: the ledger sheet of the accounting trade.</summary>
    public static Extent Ledger { get; } = FromInches(17f, 11f);

    public static Extent Executive { get; } = FromInches(7.25f, 10.5f);

    /// <summary>The ANSI architectural sizes, for drawings.</summary>
    public static Extent ArchA { get; } = FromInches(9f, 12f);
    public static Extent ArchB { get; } = FromInches(12f, 18f);
    public static Extent ArchC { get; } = FromInches(18f, 24f);
    public static Extent ArchD { get; } = FromInches(24f, 36f);
    public static Extent ArchE { get; } = FromInches(36f, 48f);
    public static Extent ArchE1 { get; } = FromInches(30f, 42f);
    public static Extent ArchE2 { get; } = FromInches(26f, 38f);
    public static Extent ArchE3 { get; } = FromInches(27f, 39f);

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
