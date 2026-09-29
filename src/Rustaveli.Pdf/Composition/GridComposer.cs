using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds a grid: cells flowing into rows of a set number of equal columns, each cell spanning one or more, a row
/// starting afresh wherever the next cell would not fit.
/// </summary>
/// <remarks>
/// Every cell in a row is as tall as the row's tallest. A row the cells do not fill sits flush left unless the grid
/// says otherwise.
/// </remarks>
public sealed class GridComposer
{
    private readonly List<ColumnSlot> _cells = [];
    private int _columns = 12;
    private float _gutter;
    private float _spaceBetweenRows;
    private HorizontalPlacement _placement = HorizontalPlacement.Left;

    internal GridComposer()
    {
    }

    /// <summary>Sets how many equal columns each row is divided into; twelve unless set.</summary>
    public void Columns(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A grid has at least one column.");

        _columns = count;
    }

    /// <summary>Sets the gap between neighbouring cells in a row.</summary>
    public void Gutter(float value) => _gutter = Numbers.NotNegative(value, nameof(value));

    /// <summary>Sets the gap between one row and the next.</summary>
    public void SpaceBetweenRows(float value) => _spaceBetweenRows = Numbers.NotNegative(value, nameof(value));

    /// <summary>Sets a row the cells do not fill against the left, the default.</summary>
    public void FlushLeft() => _placement = HorizontalPlacement.Left;

    /// <summary>Centres a row the cells do not fill.</summary>
    public void Centered() => _placement = HorizontalPlacement.Center;

    /// <summary>Sets a row the cells do not fill against the right.</summary>
    public void FlushRight() => _placement = HorizontalPlacement.Right;

    /// <summary>Adds a cell spanning <paramref name="span"/> columns and returns its frame.</summary>
    public IFrame Cell(int span = 1)
    {
        if (span < 1)
            throw new ArgumentOutOfRangeException(nameof(span), span, "A cell spans at least one column.");

        ColumnSlot cell = new ColumnSlot { Sizing = ColumnSizing.Share, Value = span };
        _cells.Add(cell);
        return cell;
    }

    /// <summary>Lays the cells out in <paramref name="stack"/>, a row of columns at a time.</summary>
    internal void Build(StackBlock stack)
    {
        stack.SpaceBetween = _spaceBetweenRows;
        ColumnsBlock? row = null;
        int used = 0;

        foreach (ColumnSlot cell in _cells)
        {
            int span = (int)cell.Value;

            if (span > _columns)
                throw new CompositionException($"A cell spans {span} columns of a grid of {_columns}.");

            if (row is null || used + span > _columns)
            {
                Close(row, used);
                row = new ColumnsBlock { Gutter = _gutter, GridColumns = _columns };
                stack.Items.Add(row);
                used = 0;
            }

            row.Items.Add(cell);
            used += span;
        }

        Close(row, used);
    }

    /// <summary>Places a row the cells do not fill, with empty columns making up the rest.</summary>
    private void Close(ColumnsBlock? row, int used)
    {
        int empty = _columns - used;

        if (row is null || empty == 0)
            return;

        switch (_placement)
        {
            case HorizontalPlacement.Center:
                row.Items.Insert(0, Empty(empty / 2f));
                row.Items.Add(Empty(empty / 2f));
                break;

            case HorizontalPlacement.Right:
                row.Items.Insert(0, Empty(empty));
                break;

            default:
                row.Items.Add(Empty(empty));
                break;
        }
    }

    private static ColumnSlot Empty(float span) => new ColumnSlot { Sizing = ColumnSizing.Share, Value = span };
}
