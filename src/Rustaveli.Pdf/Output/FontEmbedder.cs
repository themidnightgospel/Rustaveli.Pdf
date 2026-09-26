using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// The faces one document uses, each embedded once however many pages show it.
/// </summary>
internal sealed class FontEmbedder(PdfFileWriter file)
{
    private readonly Dictionary<OpenTypeFont, EmbeddedFont> _fonts = [];

    /// <summary>The embedding of <paramref name="face"/>, begun on its first use.</summary>
    public EmbeddedFont For(OpenTypeFont face)
    {
        if (!_fonts.TryGetValue(face, out EmbeddedFont? font))
        {
            font = new EmbeddedFont(face, file.Reserve());
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
