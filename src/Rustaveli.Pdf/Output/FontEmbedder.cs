using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// The faces one document uses, each embedded once however many pages show it.
/// </summary>
/// <param name="file">The file the fonts are written to.</param>
/// <param name="keepHinting">Whether TrueType glyphs keep their hinting; see <see cref="PdfExportOptions.KeepFontHinting"/>.</param>
internal sealed class FontEmbedder(PdfFileWriter file, bool keepHinting = false)
{
    private readonly Dictionary<OpenTypeFont, EmbeddedFont> _fonts = [];

    /// <summary>The embedding of <paramref name="face"/>, begun on its first use.</summary>
    public EmbeddedFont For(OpenTypeFont face)
    {
        if (!_fonts.TryGetValue(face, out EmbeddedFont? font))
        {
            font = new EmbeddedFont(face, file.Reserve(), keepHinting);
            _fonts.Add(face, font);
        }

        return font;
    }

    /// <summary>Writes every font, now that no page will use another glyph.</summary>
    public void WriteAll()
    {
        foreach (EmbeddedFont font in _fonts.Values)
            font.Write(file);
    }
}
