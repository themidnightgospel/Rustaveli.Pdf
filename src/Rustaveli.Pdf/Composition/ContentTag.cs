namespace Rustaveli.Pdf;

/// <summary>
/// The part content plays in a document's structure — a heading, a paragraph, a figure and what it shows — as a
/// tagged PDF records it for screen readers, reflow and search.
/// </summary>
/// <remarks>
/// Tags are the standard structure types of PDF, named here in words: <see cref="Paragraph"/> is <c>P</c>,
/// <see cref="Heading"/> of level 1 is <c>H1</c>. They are written only when the export is tagged; otherwise tagged
/// content is drawn exactly as it would be untagged.
/// </remarks>
public sealed class ContentTag
{
    private ContentTag(string role, string? alternateText = null, string? expansion = null)
    {
        Role = role;
        AlternateText = alternateText;
        Expansion = expansion;
    }

    /// <summary>The name PDF gives the tag, such as <c>P</c>, <c>H1</c> or <c>Figure</c>.</summary>
    public string Role { get; }

    /// <summary>What a figure or formula shows, read in its place; null for other tags.</summary>
    public string? AlternateText { get; }

    /// <summary>The words an abbreviation stands for, read in its place; null for other tags.</summary>
    public string? Expansion { get; }

    /// <summary>A section of a chapter or article, usually opening with a heading.</summary>
    public static ContentTag Section { get; } = new ContentTag("Sect");

    /// <summary>A piece complete in itself, such as a story in a newspaper.</summary>
    public static ContentTag Article { get; } = new ContentTag("Art");

    /// <summary>A group of content with no more particular part to play.</summary>
    public static ContentTag Division { get; } = new ContentTag("Div");

    /// <summary>One or more paragraphs quoted from elsewhere.</summary>
    public static ContentTag BlockQuote { get; } = new ContentTag("BlockQuote");

    /// <summary>The caption of a figure or table.</summary>
    public static ContentTag Caption { get; } = new ContentTag("Caption");

    /// <summary>An index of terms and where they appear.</summary>
    public static ContentTag Index { get; } = new ContentTag("Index");

    /// <summary>A table of contents, made of <see cref="ContentsEntry"/> content and nested tables of contents.</summary>
    public static ContentTag Contents { get; } = new ContentTag("TOC");

    /// <summary>One entry in a table of contents.</summary>
    public static ContentTag ContentsEntry { get; } = new ContentTag("TOCI");

    /// <summary>
    /// A paragraph. Text that is not inside other tagged content is tagged a paragraph without asking.
    /// </summary>
    public static ContentTag Paragraph { get; } = new ContentTag("P");

    /// <summary>A list, made of <see cref="ListItem"/> content. Lists composed as lists are tagged without asking.</summary>
    public static ContentTag List { get; } = new ContentTag("L");

    /// <summary>One item of a list: its <see cref="ListLabel"/> and its <see cref="ListBody"/>.</summary>
    public static ContentTag ListItem { get; } = new ContentTag("LI");

    /// <summary>The bullet or number of a list item.</summary>
    public static ContentTag ListLabel { get; } = new ContentTag("Lbl");

    /// <summary>The content of a list item, beside its label.</summary>
    public static ContentTag ListBody { get; } = new ContentTag("LBody");

    /// <summary>
    /// A table of data. A table composed inside is tagged row by row and cell by cell, its header rows as headings of
    /// their columns.
    /// </summary>
    public static ContentTag Table { get; } = new ContentTag("Table");

    /// <summary>Words quoted within a paragraph.</summary>
    public static ContentTag Quote { get; } = new ContentTag("Quote");

    /// <summary>Computer code.</summary>
    public static ContentTag Code { get; } = new ContentTag("Code");

    /// <summary>A footnote or endnote.</summary>
    public static ContentTag Note { get; } = new ContentTag("Note");

    /// <summary>Words within a paragraph set apart for no more particular reason, such as a change of language.</summary>
    public static ContentTag Span { get; } = new ContentTag("Span");

    /// <summary>A heading, from level 1 for the outermost to level 6.</summary>
    public static ContentTag Heading(int level)
    {
        if (level is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Heading levels run from 1 to 6.");

        return new ContentTag("H" + level.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>An abbreviation, read as the words in <paramref name="expansion"/>.</summary>
    public static ContentTag Abbreviation(string expansion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expansion);
        return new ContentTag("Span", expansion: expansion);
    }

    /// <summary>
    /// A picture, chart or diagram, read as <paramref name="alternateText"/>. Everything drawn inside belongs to it;
    /// images and drawings outside any figure are taken as decoration.
    /// </summary>
    public static ContentTag Figure(string alternateText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alternateText);
        return new ContentTag("Figure", alternateText);
    }

    /// <summary>A mathematical formula, read as <paramref name="alternateText"/>.</summary>
    public static ContentTag Formula(string alternateText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alternateText);
        return new ContentTag("Formula", alternateText);
    }

    public override string ToString() => Role;
}
