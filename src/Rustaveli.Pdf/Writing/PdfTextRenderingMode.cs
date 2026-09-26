namespace Rustaveli.Pdf.Writing;

/// <summary>Whether glyphs are filled, stroked, added to the clip, or not painted at all (ISO 32000-1, 9.3.6).</summary>
internal enum PdfTextRenderingMode
{
    Fill = 0,
    Stroke = 1,
    FillAndStroke = 2,

    /// <summary>Neither filled nor stroked: text that can be selected and searched but is not seen.</summary>
    Invisible = 3,

    FillAndClip = 4,
    StrokeAndClip = 5,
    FillStrokeAndClip = 6,
    Clip = 7,
}
