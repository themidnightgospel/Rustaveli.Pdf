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
    public RunComposer Folio() => Folio(Numerals.Arabic);

    /// <summary>
    /// Appends the number of the page this text is drawn on, written by <paramref name="format"/> — such as
    /// <see cref="Numerals.LowerRoman"/> for front matter.
    /// </summary>
    public RunComposer Folio(Func<int, string> format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Add(new TextRun
        {
            DynamicText = page => format(page.Folio)
        });
    }

    /// <summary>
    /// Appends the total number of pages in the document. Resolves to a provisional value during the counting
    /// pass and to the true total when the document is drawn.
    /// </summary>
    public RunComposer PageCount() => PageCount(Numerals.Arabic);

    /// <summary>Appends the total number of pages in the document, written by <paramref name="format"/>.</summary>
    public RunComposer PageCount(Func<int, string> format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Add(new TextRun
        {
            DynamicText = page => format(page.PageCount)
        });
    }

    /// <summary>Appends the number of the page an anchor is on, or "?" until it has been reached.</summary>
    public RunComposer FolioOf(string anchor) => FolioOf(anchor, Numerals.Arabic);

    /// <summary>Appends the number of the page an anchor is on, written by <paramref name="format"/>.</summary>
    public RunComposer FolioOf(string anchor, Func<int, string> format) =>
        Anchored(anchor, format, page => page.FolioOf(anchor));

    /// <summary>
    /// Appends the number of the page an anchor's content ends on — the last page of a chapter anchored by a
    /// frame that flows across several — or "?" until it has been reached.
    /// </summary>
    public RunComposer LastFolioOf(string anchor) => LastFolioOf(anchor, Numerals.Arabic);

    /// <summary>Appends the number of the page an anchor's content ends on, written by <paramref name="format"/>.</summary>
    public RunComposer LastFolioOf(string anchor, Func<int, string> format) =>
        Anchored(anchor, format, page => page.LastFolioOf(anchor));

    /// <summary>
    /// Appends this page's number counted from the page an anchor begins on — "page 2" of a chapter — or "?" until
    /// the anchor has been reached.
    /// </summary>
    public RunComposer FolioWithin(string anchor) => FolioWithin(anchor, Numerals.Arabic);

    /// <summary>Appends this page's number counted from the page an anchor begins on, written by <paramref name="format"/>.</summary>
    public RunComposer FolioWithin(string anchor, Func<int, string> format) =>
        Anchored(anchor, format, page => page.FolioOf(anchor) is int first ? page.Folio - first + 1 : null);

    /// <summary>Appends how many pages an anchor's content spans, or "?" until it has been reached.</summary>
    public RunComposer PageCountOf(string anchor) => PageCountOf(anchor, Numerals.Arabic);

    /// <summary>Appends how many pages an anchor's content spans, written by <paramref name="format"/>.</summary>
    public RunComposer PageCountOf(string anchor, Func<int, string> format) =>
        Anchored(anchor, format, page => page.FolioOf(anchor) is int first && page.LastFolioOf(anchor) is int last ? last - first + 1 : null);

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
    public RunComposer CrossReference(string text, string anchor)
    {
        return Add(new TextRun
        {
            Text = text,
            Anchor = anchor
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

    /// <summary>A number read from where an anchor fell, written by <paramref name="format"/>; "?" until it is known.</summary>
    private RunComposer Anchored(string anchor, Func<int, string> format, Func<Pagination, int?> number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchor);
        ArgumentNullException.ThrowIfNull(format);

        return Add(new TextRun
        {
            DynamicText = page => number(page) is int value ? format(value) : "?"
        });
    }
}
