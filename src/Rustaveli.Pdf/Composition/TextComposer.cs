using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds a paragraph out of styled spans.
/// </summary>
public sealed class TextComposer
{
    private readonly TextBlock _block;

    internal TextComposer(TextBlock block) => _block = block;

    /// <summary>Appends a run of text.</summary>
    public RunComposer Run(string text)
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
    public void BlankLine()
    {
        Add(new TextRun
        {
            Text = "\n"
        });
    }

    /// <summary>Appends the number of the page this text is drawn on.</summary>
    public RunComposer Folio()
    {
        return Add(new TextRun
        {
            DynamicText = (Pagination page) => page.Folio.ToString()
        });
    }

    /// <summary>
    /// Appends the total number of pages in the document. Resolves to a provisional value during the counting
    /// pass and to the true total when the document is drawn.
    /// </summary>
    public RunComposer PageCount()
    {
        return Add(new TextRun
        {
            DynamicText = (Pagination page) => page.PageCount.ToString()
        });
    }

    /// <summary>Appends the page number a named section resolved to, or "?" if it has not been reached yet.</summary>
    public RunComposer FolioOf(string sectionName)
    {
        return Add(new TextRun
        {
            DynamicText = (Pagination page) => page.FolioOf(sectionName)?.ToString() ?? "?"
        });
    }

    /// <summary>Appends text that opens an external URL when clicked.</summary>
    public RunComposer Link(string text, string url)
    {
        return Add(new TextRun
        {
            Text = text,
            Url = url
        });
    }

    /// <summary>Appends text that jumps to a named section when clicked.</summary>
    public RunComposer CrossReference(string text, string sectionName)
    {
        return Add(new TextRun
        {
            Text = text,
            Anchor = sectionName
        });
    }

    /// <summary>
    /// Places content inline among the words — an icon, a logo, a small chart.
    /// </summary>
    /// <remarks>
    /// The element behaves as one unbreakable word: it rests on the baseline, moves to the next line whole if it
    /// does not fit, and raises the line it lands on to accommodate its height.
    /// </remarks>
    public void Inline(Action<IFrame> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Frame container = new Frame();
        handler(container);
        if (container.Child != null)
        {
            _block.Runs.Add(new TextRun
            {
                Inline = container
            });
        }
    }

    /// <summary>Indents the opening line of every paragraph in this block.</summary>
    public void FirstLineIndent(float indent)
    {
        _block.FirstLineIndent = indent;
    }

    /// <summary>Inserts a vertical gap before every paragraph after the first.</summary>
    public void SpaceBetweenParagraphs(float spacing)
    {
        _block.SpaceBetweenParagraphs = spacing;
    }

    public void FlushLeft()
    {
        _block.Alignment = HorizontalPlacement.Left;
    }

    public void Centered()
    {
        _block.Alignment = HorizontalPlacement.Center;
    }

    public void FlushRight()
    {
        _block.Alignment = HorizontalPlacement.Right;
    }

    /// <summary>Adjusts the style inherited by every span in this paragraph.</summary>
    public void DefaultType(Func<TypeStyle, TypeStyle> refinement)
    {
        Func<TypeStyle, TypeStyle>? previous = _block.DefaultTypeRefinement;

        _block.DefaultTypeRefinement = previous is null
            ? refinement
            : style => refinement(previous(style));
    }

    private RunComposer Add(TextRun span)
    {
        _block.Runs.Add(span);
        return new RunComposer(span);
    }
}
