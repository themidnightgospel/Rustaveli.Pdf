using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds a paragraph out of styled spans.
/// </summary>
public sealed class TextDescriptor(TextElement element)
{
    /// <summary>Appends a run of text.</summary>
    public TextSpanDescriptor Span(string text)
    {
        return Add(new TextSpan
        {
            Text = text
        });
    }

    /// <summary>Appends a run of text followed by a line break.</summary>
    public TextSpanDescriptor Line(string text)
    {
        return Add(new TextSpan
        {
            Text = text + "\n"
        });
    }

    /// <summary>Appends a blank line.</summary>
    public void EmptyLine()
    {
        Add(new TextSpan
        {
            Text = "\n"
        });
    }

    /// <summary>Appends the number of the page this text is drawn on.</summary>
    public TextSpanDescriptor CurrentPageNumber()
    {
        return Add(new TextSpan
        {
            DynamicText = (PageContext page) => page.CurrentPage.ToString()
        });
    }

    /// <summary>
    /// Appends the total number of pages in the document. Resolves to a provisional value during the counting
    /// pass and to the true total when the document is drawn.
    /// </summary>
    public TextSpanDescriptor TotalPages()
    {
        return Add(new TextSpan
        {
            DynamicText = (PageContext page) => page.TotalPages.ToString()
        });
    }

    /// <summary>Appends the page number a named section resolved to, or "?" if it has not been reached yet.</summary>
    public TextSpanDescriptor PageNumberOfSection(string sectionName)
    {
        return Add(new TextSpan
        {
            DynamicText = (PageContext page) => page.GetDestinationPage(sectionName)?.ToString() ?? "?"
        });
    }

    /// <summary>Appends text that opens an external URL when clicked.</summary>
    public TextSpanDescriptor Hyperlink(string text, string url)
    {
        return Add(new TextSpan
        {
            Text = text,
            Url = url
        });
    }

    /// <summary>Appends text that jumps to a named section when clicked.</summary>
    public TextSpanDescriptor SectionLink(string text, string sectionName)
    {
        return Add(new TextSpan
        {
            Text = text,
            Destination = sectionName
        });
    }

    /// <summary>
    /// Places content inline among the words — an icon, a logo, a small chart.
    /// </summary>
    /// <remarks>
    /// The element behaves as one unbreakable word: it rests on the baseline, moves to the next line whole if it
    /// does not fit, and raises the line it lands on to accommodate its height.
    /// </remarks>
    public void Element(Action<IContainer> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        Container container = new Container();
        handler(container);
        if (container.Child != null)
        {
            element.Spans.Add(new TextSpan
            {
                InlineElement = container
            });
        }
    }

    /// <summary>Indents the opening line of every paragraph in this block.</summary>
    public void FirstLineIndent(float indent)
    {
        element.FirstLineIndent = indent;
    }

    /// <summary>Inserts a vertical gap before every paragraph after the first.</summary>
    public void ParagraphSpacing(float spacing)
    {
        element.ParagraphSpacing = spacing;
    }

    public void AlignLeft()
    {
        element.Alignment = HorizontalAlignment.Left;
    }

    public void AlignCenter()
    {
        element.Alignment = HorizontalAlignment.Center;
    }

    public void AlignRight()
    {
        element.Alignment = HorizontalAlignment.Right;
    }

    /// <summary>Adjusts the style inherited by every span in this paragraph.</summary>
    public void DefaultTextStyle(Func<TextStyle, TextStyle> refinement)
    {
        Func<TextStyle, TextStyle>? previous = element.DefaultStyleOverride;

        element.DefaultStyleOverride = previous is null
            ? refinement
            : style => refinement(previous(style));
    }

    private TextSpanDescriptor Add(TextSpan span)
    {
        element.Spans.Add(span);
        return new TextSpanDescriptor(span);
    }
}
