using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A grid of cells with independently sized columns, repeating header and footer bands, and automatic
/// pagination at row boundaries.
/// </summary>
/// <remarks>
/// Rows are atomic: a row is never split across pages, and a group of rows joined by a vertical span moves as a
/// unit. This keeps cell borders and backgrounds coherent, at the cost of leaving whitespace when a very tall
/// row does not fit.
/// </remarks>
internal sealed class TableBlock : Block
{
    private sealed class TableLayout
    {
        public required float[] ColumnWidths { get; init; }

        public required float[] ColumnOffsets { get; init; }

        public required float TotalWidth { get; init; }

        public required ReadingDirection Direction { get; init; }

        public float[] HeaderHeights { get; set; } = Array.Empty<float>();

        public float[] FooterHeights { get; set; } = Array.Empty<float>();

        public float[] BodyHeights { get; set; } = Array.Empty<float>();

        public int[] GroupEnd { get; set; } = Array.Empty<int>();

        public float HeaderHeight => HeaderHeights.Sum();

        public float FooterHeight => FooterHeights.Sum();

        /// <summary>Combined height of the bands that repeat on every page.</summary>
        public float BandHeight => HeaderHeight + FooterHeight;

        /// <summary>
        /// The left edge of a cell. Right-to-left tables mirror the whole grid, so column one sits against the
        /// right edge and a spanning cell is offset by its combined width rather than its first column's.
        /// </summary>
        public float ColumnLeft(CellBlock cell, float spanWidth) =>
            Direction == Pdf.ReadingDirection.LeftToRight
                ? ColumnOffsets[cell.Column - 1]
                : TotalWidth - ColumnOffsets[cell.Column - 1] - spanWidth;

        /// <summary>The combined width of every column a cell spans, clamped to the columns that exist.</summary>
        public float SpanWidth(CellBlock cell)
        {
            float width = 0f;

            for (int column = cell.Column; column <= Math.Min(cell.LastColumn, ColumnWidths.Length); column++)
                width += ColumnWidths[column - 1];

            return width;
        }
    }

    private int _completedRows;

    private TableLayout? _cachedLayout;

    private float _cachedWidth = float.NaN;

    private ReadingDirection _cachedDirection;

    /// <summary>The table's head, body and foot in a tagged document, once it has begun drawing.</summary>
    private StructureElement?[]? _groups;

    /// <summary>The element of each cell drawn tagged.</summary>
    private Dictionary<CellBlock, StructureElement>? _cellTags;

    /// <summary>Overrides the inherited flow direction, reversing column order. Null follows the context.</summary>
    public ReadingDirection? ReadingDirection { get; set; }

    public List<TableColumnSpec> Columns { get; } = [];

    /// <summary>Whether the last body cell of each column reaches the bottom of the rows drawn on each page.</summary>
    public bool ExtendLastCells { get; set; }

    public List<CellBlock> Cells { get; } = new List<CellBlock>();

    /// <summary>Rows repeated at the top of every page the table spans.</summary>
    public List<CellBlock> HeaderCells { get; } = new List<CellBlock>();

    /// <summary>Rows repeated at the bottom of every page the table spans.</summary>
    public List<CellBlock> FooterCells { get; } = new List<CellBlock>();

    public override IEnumerable<Block?> GetChildren()
    {
        return Cells.Concat(HeaderCells).Concat(FooterCells);
    }

    protected override void ResetOwnState()
    {
        _completedRows = 0;
        _cachedLayout = null;
        _cachedWidth = float.NaN;
        _groups = null;
        _cellTags = null;
    }

    protected override object? SaveOwnProgress() => (_completedRows, _cachedLayout, _cachedWidth, _cachedDirection);

    protected override void RestoreOwnProgress(object progress) =>
        (_completedRows, _cachedLayout, _cachedWidth, _cachedDirection) = ((int, TableLayout?, float, ReadingDirection))progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        TableLayout? layout = BuildLayout(availableSpace, context);

        if (layout is null)
            return Fit.Defer("The table columns do not fit within the available width.");

        if (_completedRows >= layout.BodyHeights.Length)
            return Fit.Nothing();

        if (layout.BandHeight > availableSpace.Height + Extent.Epsilon)
            return Fit.Defer("The header and footer rows alone exceed the available height.");

        (float takenHeight, int lastRow) = TakeRows(layout, availableSpace.Height);

        if (lastRow == _completedRows)
            return Fit.Defer("The next table row is taller than the available height.");

        Extent size = new Extent(layout.TotalWidth, layout.BandHeight + takenHeight);

        return lastRow >= layout.BodyHeights.Length
            ? Fit.Complete(size)
            : Fit.Partial(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        TableLayout? layout = BuildLayout(availableSpace, context.Planning);

        if (layout is null
            || _completedRows >= layout.BodyHeights.Length
            || layout.BandHeight > availableSpace.Height + Extent.Epsilon)
        {
            return;
        }

        (float takenHeight, int lastRow) = TakeRows(layout, availableSpace.Height);

        if (lastRow == _completedRows)
            return;

        float top = 0f;

        // Drawn straight inside a table tag, the table tags its rows and cells. The bands that repeat are read once,
        // where they are first drawn, and are decoration on every page after.
        TagStack tags = context.Tags;
        bool tagging = tags.Enabled && !tags.IsUntagged && tags.Current.Role == "Table";
        bool repeat = _completedRows > 0;

        if (tagging && _groups is null)
        {
            _groups =
            [
                HeaderCells.Count > 0 ? tags.Create("THead") : null,
                tags.Create("TBody"),
                FooterCells.Count > 0 ? tags.Create("TFoot") : null,
            ];
        }

        StructureElement? head = tagging && !repeat ? _groups![0] : null;
        StructureElement? body = tagging ? _groups![1] : null;
        StructureElement? foot = tagging && !repeat ? _groups![2] : null;

        if (layout.HeaderHeights.Length != 0)
        {
            using (tagging && repeat ? tags.Untag() : default(TagStack.Scope))
                DrawBand(HeaderCells, layout.HeaderHeights, layout, 1, layout.HeaderHeights.Length, top, context, head, heads: true);

            top += layout.HeaderHeight;
        }

        DrawBand(Cells, layout.BodyHeights, layout, _completedRows + 1, lastRow, top, context, body, heads: false, ExtendLastCells);
        top += takenHeight;

        if (layout.FooterHeights.Length != 0)
        {
            using TagStack.Scope scope = tagging && repeat ? tags.Untag() : default;
            DrawBand(FooterCells, layout.FooterHeights, layout, 1, layout.FooterHeights.Length, top, context, foot, heads: false);
        }

        _completedRows = lastRow;
        ResetRepeatingBands();
    }

    /// <summary>
    /// Clears the pagination state the header and footer content accumulated while drawing this page.
    /// </summary>
    /// <remarks>
    /// These bands repeat in full on every page, but their content does not know that: a text cell that has
    /// drawn all of its lines reports itself exhausted and would render nothing on the next page. Resetting
    /// after drawing — rather than before measuring — keeps measurement free of side effects. Document-wide
    /// state is preserved so a "show once" marker inside a header still appears only once.
    /// </remarks>
    private void ResetRepeatingBands()
    {
        foreach (CellBlock headerCell in HeaderCells)
        {
            headerCell.ResetState(includeDocumentProgress: false);
        }
        foreach (CellBlock footerCell in FooterCells)
        {
            footerCell.ResetState(includeDocumentProgress: false);
        }
    }

    /// <summary>
    /// Chooses how many body rows fit in the space left once the repeating bands are accounted for: whole rows are
    /// accumulated from the current position until the next would overflow, never stopping inside a vertically
    /// spanned group.
    /// </summary>
    /// <remarks>
    /// A row is only committed once the whole vertically-spanned group it belongs to fits, so a cell spanning
    /// three rows never has its first row left on one page and its remainder on the next.
    /// </remarks>
    private (float Height, int LastRow) TakeRows(TableLayout layout, float availableHeight)
    {
        float bodySpace = availableHeight - layout.BandHeight;
        float accumulated = 0f;
        int lastRow = _completedRows;

        for (int row = _completedRows + 1; row <= layout.BodyHeights.Length; row++)
        {
            float candidate = accumulated + layout.BodyHeights[row - 1];

            if (candidate > bodySpace + Extent.Epsilon)
                break;

            accumulated = candidate;

            // Only advance the commit point to rows that close their span group; a row in the middle of one
            // has fitted, but taking it would split the group across the page break.
            if (layout.GroupEnd[row - 1] <= row)
                lastRow = row;
        }

        if (lastRow == _completedRows)
            return (Height: 0f, LastRow: _completedRows);

        float height = 0f;

        for (int row = _completedRows + 1; row <= lastRow; row++)
            height += layout.BodyHeights[row - 1];

        return (Height: height, LastRow: lastRow);
    }

    private void DrawBand(
        List<CellBlock> cells,
        float[] rowHeights,
        TableLayout layout,
        int firstRow,
        int lastRow,
        float bandTop,
        RenderContext context,
        StructureElement? group,
        bool heads,
        bool extendLastCells = false)
    {
        if (group is not null)
            TagCells(cells, firstRow, lastRow, group, heads, context.Tags);

        foreach (CellBlock cell in cells)
        {
            if (cell.Row < firstRow || cell.Row > lastRow)
                continue;

            StructureElement? element = null;
            _cellTags?.TryGetValue(cell, out element);
            using TagStack.Scope scope = context.Tags.Enter(element);

            // The last cell of its columns reaches down to the last row drawn here.
            int bottomRow = extendLastCells && IsLastInItsColumns(cell, cells) ? lastRow : cell.LastRow;

            float cellTop = bandTop;

            for (int row = firstRow; row < cell.Row; row++)
                cellTop += rowHeights[row - 1];

            // A vertically spanning cell is as tall as the rows it covers, clamped to the band actually being
            // drawn so a span reaching past this page does not overflow it.
            float cellHeight = 0f;

            for (int row = cell.Row; row <= Math.Min(bottomRow, lastRow); row++)
                cellHeight += rowHeights[row - 1];

            Extent cellSpace = new Extent(layout.SpanWidth(cell), cellHeight);
            Offset offset = new Offset(layout.ColumnLeft(cell, cellSpace.Width), cellTop);

            context.Surface.Translate(offset);
            context.RenderAllotted(cell, cellSpace, Extent.Max.Height);
            context.Surface.Translate(offset.Reverse());
        }
    }

    /// <summary>
    /// Creates the rows about to be drawn in <paramref name="group"/>, in order, and their cells in column order:
    /// headings of their columns in a header band, of their rows where marked, data otherwise.
    /// </summary>
    private void TagCells(List<CellBlock> cells, int firstRow, int lastRow, StructureElement group, bool heads, TagStack tags)
    {
        _cellTags ??= [];
        using TagStack.Scope inGroup = tags.Enter(group);

        IEnumerable<IGrouping<int, CellBlock>> rows = cells
            .Where(cell => cell.Row >= firstRow && cell.Row <= lastRow)
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .GroupBy(cell => cell.Row);

        foreach (IGrouping<int, CellBlock> row in rows)
        {
            using TagStack.Scope inRow = tags.Enter(tags.Create("TR"));

            foreach (CellBlock cell in row)
            {
                StructureElement element = tags.Create(heads || cell.HeadsRow ? "TH" : "TD")!;
                element.Scope = heads ? TableScope.Column : cell.HeadsRow ? TableScope.Row : null;
                element.RowSpan = Math.Max(1, cell.RowSpan);
                element.ColumnSpan = Math.Max(1, cell.ColumnSpan);
                _cellTags[cell] = element;
            }
        }
    }

    /// <summary>Whether no other cell starts below <paramref name="cell"/> in any column it covers.</summary>
    private static bool IsLastInItsColumns(CellBlock cell, List<CellBlock> cells)
    {
        foreach (CellBlock other in cells)
        {
            if (other.Row > cell.LastRow && other.Column <= cell.LastColumn && other.LastColumn >= cell.Column)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves column widths and row heights, reusing the previous result when nothing they depend on has
    /// changed.
    /// </summary>
    /// <remarks>
    /// Caching is not merely an optimisation here, it is a correctness improvement. Row heights depend only on
    /// the content and the column widths, both of which are fixed for the life of a render pass — but measuring
    /// a cell that has already been drawn returns Empty, so recomputing on a later page would report the wrong
    /// heights for rows behind the cursor. Computing once, while every cell is still fresh, avoids that.
    ///
    /// Without it the cost is quadratic: every page re-measures every cell in the table, and the number of pages
    /// grows with the number of rows.
    /// </remarks>
    private TableLayout? BuildLayout(Extent availableSpace, PlanContext context)
    {
        ReadingDirection direction = ReadingDirection ?? context.ReadingDirection;

        if (_cachedLayout is not null
            && Math.Abs(_cachedWidth - availableSpace.Width) < Extent.Epsilon
            && _cachedDirection == direction)
        {
            // The bands are deliberately outside the cache — see MeasureBands.
            MeasureBands(_cachedLayout, context);
            return _cachedLayout;
        }

        float[]? columnWidths = ResolveColumnWidths(availableSpace.Width);

        if (columnWidths is null)
            return null;

        float[] columnOffsets = new float[columnWidths.Length];

        for (int column = 1; column < columnWidths.Length; column++)
            columnOffsets[column] = columnOffsets[column - 1] + columnWidths[column - 1];

        TableLayout layout = new TableLayout
        {
            ColumnWidths = columnWidths,
            ColumnOffsets = columnOffsets,
            TotalWidth = columnWidths.Sum(),
            Direction = direction
        };

        layout.BodyHeights = MeasureRowHeights(Cells, layout, context);
        layout.GroupEnd = BuildGroupBoundaries(Cells, layout.BodyHeights.Length);

        _cachedLayout = layout;
        _cachedWidth = availableSpace.Width;
        _cachedDirection = direction;

        MeasureBands(layout, context);

        return layout;
    }

    /// <summary>
    /// Measures the repeating bands, which — unlike the body — must be re-measured for every page.
    /// </summary>
    /// <remarks>
    /// Band content is reset between pages so it can repeat, and its height is legitimately allowed to differ:
    /// a "continued" marker suppressed on the opening page makes the header shorter there than everywhere else.
    /// Caching band heights alongside the body would freeze page one's answer and the marker would never appear.
    /// The bands are small, so recomputing them costs nothing next to the body.
    /// </remarks>
    private void MeasureBands(TableLayout layout, PlanContext context)
    {
        layout.HeaderHeights = MeasureRowHeights(HeaderCells, layout, context);
        layout.FooterHeights = MeasureRowHeights(FooterCells, layout, context);
    }

    /// <summary>
    /// For each row, the last row it is bound to by a vertical span. Rows may only be separated where this
    /// equals the row itself.
    /// </summary>
    private static int[] BuildGroupBoundaries(List<CellBlock> cells, int rowCount)
    {
        int[] groupEnd = new int[rowCount];

        // A row with no span is its own group, so it may be broken after.
        for (int row = 1; row <= rowCount; row++)
            groupEnd[row - 1] = row;

        foreach (CellBlock cell in cells)
        {
            if (cell.RowSpan <= 1)
                continue;

            // The row count is the last row any cell reaches, so no span runs past it.
            for (int row = cell.Row; row <= cell.LastRow; row++)
                groupEnd[row - 1] = Math.Max(groupEnd[row - 1], cell.LastRow);
        }

        return groupEnd;
    }

    /// <summary>
    /// Measures every cell at its natural height, then distributes the height of vertically spanned cells across
    /// the rows they cover.
    /// </summary>
    private static float[] MeasureRowHeights(List<CellBlock> cells, TableLayout layout, PlanContext context)
    {
        if (cells.Count == 0)
            return [];

        int rowCount = cells.Max(cell => cell.LastRow);
        float[] heights = new float[rowCount];

        // Unspanned cells set the baseline height of the row they sit in.
        foreach (CellBlock cell in cells.Where(cell => cell.RowSpan <= 1))
        {
            Fit plan = cell.Plan(new Extent(layout.SpanWidth(cell), Extent.Max.Height), context);

            if (!plan.IsDeferred)
                heights[cell.Row - 1] = Math.Max(heights[cell.Row - 1], plan.Size.Height);
        }

        // A spanned cell only forces growth when the rows it covers cannot already hold it, and the shortfall
        // goes on its *last* row: charging an earlier one would push the rows below it down inside the span.
        foreach (CellBlock cell in cells.Where(cell => cell.RowSpan > 1))
        {
            Fit plan = cell.Plan(new Extent(layout.SpanWidth(cell), Extent.Max.Height), context);

            if (plan.IsDeferred)
                continue;

            float spannedHeight = 0f;

            for (int row = cell.Row; row <= cell.LastRow; row++)
                spannedHeight += heights[row - 1];

            if (plan.Size.Height > spannedHeight)
                heights[cell.LastRow - 1] += plan.Size.Height - spannedHeight;
        }

        return heights;
    }

    /// <summary>Splits the available width across columns: constants keep their size, relatives share the rest.</summary>
    /// <remarks>Null means the table cannot be laid out at this width, which the caller turns into a wrap.</remarks>
    private float[]? ResolveColumnWidths(float availableWidth)
    {
        if (Columns.Count == 0)
            return null;

        float constantWidth = Columns.Where(column => !column.TakesShare).Sum(column => Math.Max(0f, column.Value));

        if (constantWidth > availableWidth + Extent.Epsilon)
            return null;

        float totalWeight = Columns.Where(column => column.TakesShare).Sum(column => Math.Max(0f, column.Value));
        float leftover = Math.Max(0f, availableWidth - constantWidth);
        float[] widths = new float[Columns.Count];

        for (int index = 0; index < Columns.Count; index++)
        {
            TableColumnSpec column = Columns[index];

            widths[index] = column.TakesShare
                ? (totalWeight > 0f ? leftover * Math.Max(0f, column.Value) / totalWeight : 0f)
                : Math.Max(0f, column.Value);
        }

        return widths;
    }
}
