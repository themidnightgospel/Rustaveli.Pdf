using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds a paragraph out of styled spans.
/// </summary>
public sealed class TextComposer(TextBlock element)
{
    /// <summary>Appends a run of text.</summary>
    public RunComposer Span(string text)
    {
        return Add(new TextRun
        {
            Text = text
        });
    }

    /// <summary>Appends a run of text followed by a line break.</summary>
    public RunComposer Line(string text)
    {
        return Add(new TextRun
        {
            Text = text + "\n"
        });
    }

    /// <summary>Appends a blank line.</summary>
    public void EmptyLine()
    {
        Add(new TextRun
        {
            Text = "\n"
        });
    }

    /// <summary>Appends the number of the page this text is drawn on.</summary>
    public RunComposer CurrentPageNumber()
    {
        return Add(new TextRun
        {
            DynamicText = (Pagination page) => page.CurrentPage.ToString()
        });
    }

    /// <summary>
    /// Appends the total number of pages in the document. Resolves to a provisional value during the counting
    /// pass and to the true total when the document is drawn.
    /// </summary>
    public RunComposer TotalPages()
    {
        return Add(new TextRun
        {
            DynamicText = (Pagination page) => page.TotalPages.ToString()
        });
    }

    /// <summary>Appends the page number a named section resolved to, or "?" if it has not been reached yet.</summary>
    public RunComposer PageNumberOfSection(string sectionName)
    {
        return Add(new TextRun
        {
            DynamicText = (Pagination page) => page.GetDestinationPage(sectionName)?.ToString() ?? "?"
        });
    }

    /// <summary>Appends text that opens an external URL when clicked.</summary>
    public RunComposer Hyperlink(string text, string url)
    {
        return Add(new TextRun
        {
            Text = text,
            Url = url
        });
    }

    /// <summary>Appends text that jumps to a named section when clicked.</summary>
    public RunComposer SectionLink(string text, string sectionName)
    {
        return Add(new TextRun
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
    public void Element(Action<IFrame> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        Frame container = new Frame();
        handler(container);
        if (container.Child != null)
        {
            element.Spans.Add(new TextRun
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
        element.Alignment = HorizontalPlacement.Left;
    }

    public void AlignCenter()
    {
        element.Alignment = HorizontalPlacement.Center;
    }

    public void AlignRight()
    {
        element.Alignment = HorizontalPlacement.Right;
    }

    /// <summary>Adjusts the style inherited by every span in this paragraph.</summary>
    public void DefaultTextStyle(Func<TypeStyle, TypeStyle> refinement)
    {
        Func<TypeStyle, TypeStyle>? previous = element.DefaultStyleOverride;

        element.DefaultStyleOverride = previous is null
            ? refinement
            : style => refinement(previous(style));
    }

    private RunComposer Add(TextRun span)
    {
        element.Spans.Add(span);
        return new RunComposer(span);
    }
}
