namespace Rustaveli.Pdf.Tagging;

/// <summary>
/// One element of a document's logical structure: a paragraph, a heading, a table cell. Elements nest as content
/// nests, in the order it is read, and each holds the content drawn for it.
/// </summary>
internal sealed class StructureElement
{
    public StructureElement(string role, StructureElement? parent)
    {
        Role = role;
        Parent = parent;
    }

    /// <summary>The standard structure type, such as <c>P</c> or <c>TD</c>.</summary>
    public string Role { get; }

    public StructureElement? Parent { get; }

    /// <summary>
    /// Child elements, and whatever a surface records of the content drawn for this element, in reading order.
    /// </summary>
    public List<object> Kids { get; } = [];

    public string? AlternateText { get; set; }

    public string? Expansion { get; set; }

    /// <summary>The language of this element's content, where it differs from its parent's.</summary>
    public string? Language { get; set; }

    /// <summary>Which cells a table heading heads, for a <c>TH</c>.</summary>
    public TableScope? Scope { get; set; }

    public int RowSpan { get; set; } = 1;

    public int ColumnSpan { get; set; } = 1;

    /// <summary>
    /// Whether everything drawn for the element is its content, as for a figure. Only text is content elsewhere;
    /// images and drawings outside an illustration are decoration.
    /// </summary>
    public bool IsIllustration => Role is "Figure" or "Formula";

    /// <summary>Whether the element only groups others, so text set straight inside it wants a paragraph of its own.</summary>
    public bool IsGrouping => Role is "Document" or "Part" or "Art" or "Sect" or "Div" or "BlockQuote" or "TOC" or "Index"
        or "L" or "LI" or "Table" or "THead" or "TBody" or "TFoot" or "TR";

    /// <summary>The language in force for the element: its own, or the nearest ancestor's.</summary>
    public string? EffectiveLanguage => Language ?? Parent?.EffectiveLanguage;
}
