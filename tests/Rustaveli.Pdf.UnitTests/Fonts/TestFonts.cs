using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// The committed fonts under tests/assets/fonts, loaded once per test run. The expected values asserted against
/// them were read from the same files with fontTools, an independent parser.
/// </summary>
internal static class TestFonts
{
    public const string RegularFile = "NotoSans-Regular.ttf";
    public const string BoldFile = "NotoSans-Bold.ttf";
    public const string ItalicFile = "NotoSans-Italic.ttf";
    public const string GeorgianFile = "NotoSansGeorgian-Regular.ttf";
    public const string CollectionFile = "SpecimenSans.ttc";
    public const string CffFile = "SpecimenCff-Regular.otf";
    public const string LayoutFile = "SpecimenLayout-Regular.otf";

    private static readonly Lazy<OpenTypeFont> LazyRegular = new(() => Load(RegularFile));
    private static readonly Lazy<OpenTypeFont> LazyBold = new(() => Load(BoldFile));
    private static readonly Lazy<OpenTypeFont> LazyItalic = new(() => Load(ItalicFile));
    private static readonly Lazy<OpenTypeFont> LazyGeorgian = new(() => Load(GeorgianFile));
    private static readonly Lazy<OpenTypeFont> LazyCff = new(() => Load(CffFile));
    private static readonly Lazy<OpenTypeFont> LazyLayout = new(() => Load(LayoutFile));
    private static readonly Lazy<IReadOnlyList<OpenTypeFont>> LazySpecimens =
        new(() => OpenTypeFont.LoadAll(Bytes(CollectionFile)));

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "assets", "fonts");

    public static OpenTypeFont Regular => LazyRegular.Value;

    public static OpenTypeFont Bold => LazyBold.Value;

    public static OpenTypeFont Italic => LazyItalic.Value;

    public static OpenTypeFont Georgian => LazyGeorgian.Value;

    public static OpenTypeFont Cff => LazyCff.Value;

    /// <summary>Specimen Layout: a GSUB exercising every kind of substitution, one feature per kind.</summary>
    public static OpenTypeFont Layout => LazyLayout.Value;

    /// <summary>Specimen Sans Regular: legacy kern table, extra composite glyphs at U+E000 to U+E003.</summary>
    public static OpenTypeFont SpecimenRegular => LazySpecimens.Value[0];

    /// <summary>Specimen Sans SemiBold: typographic family names, GPOS kerning, weight 600.</summary>
    public static OpenTypeFont SpecimenSemiBold => LazySpecimens.Value[1];

    /// <summary>Specimen Sans Italic: names on the Macintosh platform only.</summary>
    public static OpenTypeFont SpecimenItalic => LazySpecimens.Value[2];

    public static string PathOf(string fileName) => Path.Combine(Directory, fileName);

    public static byte[] Bytes(string fileName) => File.ReadAllBytes(PathOf(fileName));

    public static OpenTypeFont Load(string fileName) => OpenTypeFont.Load(Bytes(fileName));
}
