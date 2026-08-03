using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// A styled fragment of a paragraph.
/// </summary>
/// <remarks>
/// Neither the text nor the style is fixed when the span is composed. Text may depend on the page being drawn,
/// which is what allows "Page 3 of 12" to exist before the total is known. Style is expressed as a modification
/// of whatever the surrounding context supplies, so a page-level default reaches every span without each one
/// having to restate it.
/// </remarks>
public sealed class TextSpan
{
    /// <summary>Literal content. Ignored when <see cref="DynamicText"/> is set.</summary>
    public string? Text { get; set; }

    /// <summary>Resolves content against the page being laid out.</summary>
    public Func<PageContext, string>? DynamicText { get; set; }

    /// <summary>Transforms the inherited style into this span's style. Null inherits unchanged.</summary>
    public Func<TextStyle, TextStyle>? StyleOverride { get; set; }

    /// <summary>Makes this span a clickable link to an external URL.</summary>
    public string? Url { get; set; }

    /// <summary>Makes this span a clickable link to a named destination inside the document.</summary>
    public string? Destination { get; set; }

    /// <summary>
    /// Content placed inline with the surrounding words rather than text.
    /// </summary>
    /// <remarks>
    /// The element is treated as a single unbreakable word: it sits on the baseline, wraps to the next line as a
    /// unit, and contributes its height to the line it lands on. Used for an icon, a logo or a small chart
    /// sitting mid-sentence.
    /// </remarks>
    public Layout.Element? InlineElement { get; set; }

    public string Resolve(PageContext page) => DynamicText?.Invoke(page) ?? Text ?? string.Empty;

    public TextStyle ResolveStyle(TextStyle inherited) => StyleOverride?.Invoke(inherited) ?? inherited;
}
