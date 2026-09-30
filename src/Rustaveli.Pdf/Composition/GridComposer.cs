using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Adds the cells of a grid: rows divided into equal columns, twelve unless set, with each cell spanning one or more
/// of them and the cells filling each row in turn.
/// </summary>
/// <remarks>
/// A cell goes into the row being filled if the columns left in it are enough for its span, and starts the next row
/// if they are not. Because every row is divided the same way, column edges line up from row to row whatever the
/// cells span. Each row is as tall as its tallest cell, and every cell in it is drawn that tall. Rows run on from page
/// to page as the items of a stack do, and read right to left, mirrored, where the reading direction says so.
/// </remarks>
public sealed class GridComposer
{
    private readonly List<(ColumnSlot Cell, int Span)> _cells = [];
    private int _columns = 12;
    private float _gutter;
    private float _rowSpace;
    private HorizontalPlacement _placement = HorizontalPlacement.Left;

    internal GridComposer()
    {
    }

    /// <summary>
    /// Sets how many equal columns each row is divided into; twelve unless set. The number set last applies, whether
    /// before the cells are added or after.
    /// </summary>
    public void Columns(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), count, "A grid has at least one column.");

        _columns = count;
    }

    /// <summary>Sets the room between neighbouring columns, in points; none unless set.</summary>
    public void Gutter(float value) => _gutter = Numbers.NotNegative(value, nameof(value));

    /// <summary>Sets the room between one row and the next, in points; none unless set.</summary>
    public void SpaceBetweenRows(float value) => _rowSpace = Numbers.NotNegative(value, nameof(value));

    /// <summary>Sets a row its cells do not fill against the start of the row. This is how rows are set unless told otherwise.</summary>
    public void FlushLeft() => _placement = HorizontalPlacement.Left;

    /// <summary>Sets a row its cells do not fill in the middle, with the columns left over shared either side.</summary>
    public void Centered() => _placement = HorizontalPlacement.Center;

    /// <summary>Sets a row its cells do not fill against the end of the row.</summary>
    public void FlushRight() => _placement = HorizontalPlacement.Right;

    /// <summary>Adds a cell spanning <paramref name="span"/> columns, and returns the frame its content goes into.</summary>
    public IFrame Cell(int span = 1)
    {
        if (span < 1)
            throw new ArgumentOutOfRangeException(nameof(span), span, "A cell spans at least one column.");

        ColumnSlot cell = new ColumnSlot { Value = span };
        _cells.Add((cell, span));
        return cell;
    }

    /// <summary>
    /// Fills <paramref name="rows"/> with the grid's rows, once the composing is done and the number of columns is
    /// settled.
    /// </summary>
    /// <exception cref="CompositionException">A cell spans more columns than the grid has.</exception>
    internal void Build(StackBlock rows)
    {
        foreach ((ColumnSlot _, int span) in _cells)
        {
            if (span > _columns)
                throw new CompositionException($"A cell spans {span} columns of a grid of {_columns}.");
        }

        rows.SpaceBetween = _rowSpace;

        ColumnsBlock? row = null;
        int used = 0;

        foreach ((ColumnSlot cell, int span) in _cells)
        {
            if (row is null || used + span > _columns)
            {
                Close(row, used);
                row = new ColumnsBlock { GridColumns = _columns, Gutter = _gutter };
                rows.Items.Add(row);
                used = 0;
            }

            row.Items.Add(cell);
            used += span;
        }

        Close(row, used);
    }

    /// <summary>Moves a row its cells do not fill along by the columns left over, as the placement asks.</summary>
    private void Close(ColumnsBlock? row, int used)
    {
        if (row is null)
            return;

        int unused = _columns - used;

        row.LeadingGridColumns = _placement switch
        {
            HorizontalPlacement.Center => unused / 2f,
            HorizontalPlacement.Right => unused,
            _ => 0f,
        };
    }
}
