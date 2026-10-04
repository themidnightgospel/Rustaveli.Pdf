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

    private readonly TagBlock _list;

    public ListBlock() => _list = new TagBlock { Tag = ContentTag.List, Child = _layout };

    private int _builtItemCount = -1;

    public List<ListEntry> Items { get; } = new List<ListEntry>();

    public ListNumbering Numbering { get; set; } = ListNumbering.Bullet;

    /// <summary>Width of the gutter the marker sits in.</summary>
    public float MarkerIndent { get; set; } = 18f;

    /// <summary>Vertical gap between items.</summary>
    public float SpaceBetween { get; set; }

    /// <summary>Style applied to the marker. Null inherits from the surrounding text style.</summary>
    public Func<TypeStyle, TypeStyle>? MarkerType { get; set; }

    internal override int ChildCount => 1;

    internal override Block? ChildAt(int index) => index == 0 ? _list : throw new ArgumentOutOfRangeException(nameof(index));

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
            ColumnsBlock row = new ColumnsBlock();
            TextBlock label = new TextBlock
            {
                DefaultTypeRefinement = MarkerType
            };
            label.Runs.Add(new TextRun
            {
                Text = listItem.Marker
            });

            // A list is a list whoever reads it, so it is tagged as one without asking: each item its label and body.
            ColumnSlot rowItem = new ColumnSlot
            {
                Sizing = ColumnSizing.Fixed,
                Value = MarkerIndent,
                Child = new TagBlock { Tag = ContentTag.ListLabel, Child = label }
            };
            ColumnSlot item = new ColumnSlot
            {
                Sizing = ColumnSizing.Share,
                Value = 1f,
                Child = new TagBlock { Tag = ContentTag.ListBody, Child = listItem }
            };
            row.Items.Add(rowItem);
            row.Items.Add(item);
            _layout.Items.Add(new TagBlock { Tag = ContentTag.ListItem, Child = row });
        }
        _builtItemCount = Items.Count;
    }

    /// <summary>
    /// Rebuilds the rows if the item list has changed since they were last assembled.
    /// </summary>
    /// <remarks>
    /// Every member needed to populate a list is public, so one can legitimately be assembled without the fluent
    /// helper that calls <see cref="ListBlock.Build" /> — and would otherwise render nothing at all, with its items
    /// unreachable from the tree of blocks.
    /// </remarks>
    private void EnsureBuilt()
    {
        if (_builtItemCount != Items.Count)
        {
            Build();
        }
    }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        EnsureBuilt();
        return _list.Plan(availableSpace, context);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        EnsureBuilt();
        _list.Render(availableSpace, context);
    }
}
