using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A sequence of marked items, ordered or unordered, that flows across pages.
/// </summary>
/// <remarks>
/// Built on the same column and row machinery as everything else: items stack vertically, and each is a marker
/// beside its content. That means pagination, spacing and nesting all behave exactly as they do elsewhere,
/// rather than being reimplemented here.
/// </remarks>
internal sealed class ListBlock : Block
{
    private readonly StackBlock _layout = new StackBlock();

    private int _builtItemCount = -1;

    public List<ListEntry> Items { get; } = new List<ListEntry>();

    public ListNumbering Numbering { get; set; } = ListNumbering.Bullet;

    /// <summary>Width of the gutter the marker sits in.</summary>
    public float MarkerIndent { get; set; } = 18f;

    /// <summary>Vertical gap between items.</summary>
    public float SpaceBetween { get; set; }

    /// <summary>Style applied to the marker. Null inherits from the surrounding text style.</summary>
    public Func<TypeStyle, TypeStyle>? MarkerType { get; set; }

    public override IEnumerable<Block?> GetChildren()
    {
        yield return _layout;
    }

    /// <summary>
    /// Assembles the marker-and-content rows. Called once composition is complete, because an item's number
    /// depends on how many items ended up in the list.
    /// </summary>
    internal void Build()
    {
        _layout.Items.Clear();
        _layout.SpaceBetween = SpaceBetween;
        for (int i = 0; i < Items.Count; i++)
        {
            ListEntry listItem = Items[i];
            listItem.Marker = ListMarkers.Format(Numbering, i + 1);
            ColumnsBlock rowElement = new ColumnsBlock();
            ColumnSlot rowItem = new ColumnSlot
            {
                Sizing = ColumnSizing.Fixed,
                Value = MarkerIndent
            };
            TextBlock textElement = new TextBlock
            {
                DefaultTypeRefinement = MarkerType
            };
            textElement.Runs.Add(new TextRun
            {
                Text = listItem.Marker
            });
            rowItem.Child = textElement;
            ColumnSlot item = new ColumnSlot
            {
                Sizing = ColumnSizing.Share,
                Value = 1f,
                Child = listItem
            };
            rowElement.Items.Add(rowItem);
            rowElement.Items.Add(item);
            _layout.Items.Add(rowElement);
        }
        _builtItemCount = Items.Count;
    }

    /// <summary>
    /// Rebuilds the rows if the item list has changed since they were last assembled.
    /// </summary>
    /// <remarks>
    /// Every member needed to populate a list is public, so one can legitimately be assembled without the fluent
    /// helper that calls <see cref="ListBlock.Build" /> — and would otherwise render nothing at all, with its items
    /// unreachable from the element tree.
    /// </remarks>
    private void EnsureBuilt()
    {
        if (_builtItemCount != Items.Count)
        {
            Build();
        }
    }

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        EnsureBuilt();
        return _layout.Plan(availableSpace, context);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        EnsureBuilt();
        _layout.Render(availableSpace, context);
    }
}
