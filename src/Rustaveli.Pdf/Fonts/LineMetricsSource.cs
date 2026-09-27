namespace Rustaveli.Pdf.Fonts;

/// <summary>Which of a font's three sets of vertical metrics its line metrics were taken from.</summary>
internal enum LineMetricsSource
{
    /// <summary>The <c>hhea</c> ascender, descender and line gap.</summary>
    HorizontalHeader,

    /// <summary>The <c>OS/2</c> sTypoAscender, sTypoDescender and sTypoLineGap.</summary>
    Typographic,

    /// <summary>The <c>OS/2</c> usWinAscent and usWinDescent, with no line gap.</summary>
    Windows
}
