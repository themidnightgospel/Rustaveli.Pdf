using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A sequence of marked items, ordered or unordered, that flows across pages.
/// </summary>
/// <remarks>
/// Built on the same column and row machinery as everything else: items stack vertically, and each is a marker
/// beside its content. That means pagination, spacing and nesting all behave exactly as they do elsewhere,
/// rather than being reimplemented here.
/// </remarks>
public sealed class ListElement : Block
{
    private readonly ColumnElement _layout = new ColumnElement();

    private int _builtItemCount = -1;

    public List<ListItem> Items { get; } = new List<ListItem>();

    public ListMarker Marker { get; set; } = ListMarker.Bullet;

    /// <summary>Width of the gutter the marker sits in.</summary>
    public float MarkerWidth { get; set; } = 18f;

    /// <summary>Vertical gap between items.</summary>
    public float Spacing { get; set; }

    /// <summary>Style applied to the marker. Null inherits from the surrounding text style.</summary>
    public Func<TypeStyle, TypeStyle>? MarkerStyle { get; set; }

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
        _layout.Spacing = Spacing;
        for (int i = 0; i < Items.Count; i++)
        {
            ListItem listItem = Items[i];
            listItem.Marker = ListMarkers.Format(Marker, i + 1);
            RowElement rowElement = new RowElement();
            RowItem rowItem = new RowItem
            {
                Sizing = RowItemSizing.Constant,
                Value = MarkerWidth
            };
            TextElement textElement = new TextElement
            {
                DefaultStyleOverride = MarkerStyle
            };
            textElement.Spans.Add(new TextRun
            {
                Text = listItem.Marker
            });
            rowItem.Child = textElement;
            RowItem item = new RowItem
            {
                Sizing = RowItemSizing.Relative,
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
    /// helper that calls <see cref="ListElement.Build" /> — and would otherwise render nothing at all, with its items
    /// unreachable from the element tree.
    /// </remarks>
    private void EnsureBuilt()
    {
        if (_builtItemCount != Items.Count)
        {
            Build();
        }
    }

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        EnsureBuilt();
        return _layout.Measure(availableSpace, context);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        EnsureBuilt();
        _layout.Draw(availableSpace, context);
    }
}
