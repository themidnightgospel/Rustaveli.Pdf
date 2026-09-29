using System.Text;

namespace Rustaveli.Pdf.Fuzzing;

/// <summary>
/// An SVG document read into artwork and drawn into a PDF, so the path data, transforms, styles and text it holds are
/// all used. The only acceptable failures are <see cref="FormatException"/>, for what is not an SVG document, and an
/// <see cref="OversetException"/>, for artwork too large for any page; drawing what was read must not fail otherwise.
/// </summary>
internal static class SvgTarget
{
    public static void Run(ReadOnlySpan<byte> input)
    {
        Artwork artwork;

        try
        {
            artwork = Artwork.FromSvg(Encoding.UTF8.GetString(input));
        }
        catch (FormatException)
        {
            return;
        }

        try
        {
            _ = Document.Compose(composition => composition.Section(section => section.Body().Artwork(artwork))).ExportPdf();
        }
        catch (OversetException)
        {
            // Artwork too large for the page: the layout says so, as it should.
        }
    }
}
