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

        public required BandCells Head { get; init; }

        public required BandCells Body { get; init; }

        public required BandCells Foot { get; init; }

        public float[] HeaderHeights { get; set; } = Array.Empty<float>();

        public float[] FooterHeights { get; set; } = Array.Empty<float>();

        public float[] BodyHeights { get; set; } = Array.Empty<float>();

        public int[] GroupEnd { get; set; } = Array.Empty<int>();

        /// <summary>Why a body cell cannot be set in the row it is given, or null when every one can.</summary>
        public string? BodyUnset { get; set; }

        /// <summary>Why a header or footer cell cannot be set in the row it is given, or null when every one can.</summary>
        public string? BandUnset { get; set; }

        /// <summary>Why some cell of the table cannot be set in the row it is given, or null when every one can.</summary>
        public string? Unset => BodyUnset ?? BandUnset;

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

    /// <summary>
    /// The cells of one band found by the row they start in, so that drawing a page looks only at the rows on it.
    /// </summary>
    internal sealed class BandCells
    {
        private readonly List<CellBlock> _cells;

        /// <summary>
        /// For a band listed out of row order, the places in its list of the cells starting in each row, in list order.
        /// Null for a band in row order, as a body almost always is: the cells starting in any rows are then one run of
        /// its list, found without an index.
        /// </summary>
        private readonly List<int>[]? _rows;

        private bool[]? _lastInColumns;

        public BandCells(List<CellBlock> cells)
        {
            _cells = cells;

            if (InRowOrder(cells))
                return;

            _rows = new List<int>[cells.Max(cell => cell.Row)];

            for (int row = 0; row < _rows.Length; row++)
                _rows[row] = [];

            for (int place = 0; place < cells.Count; place++)
                _rows[cells[place].Row - 1].Add(place);
        }

        /// <summary>
        /// The places in the band's list of the cells starting in rows <paramref name="firstRow"/> to
        /// <paramref name="lastRow"/>, in list order, as a walk of the whole list would find them.
        /// </summary>
        public BandPlaces Starting(int firstRow, int lastRow)
        {
            if (_rows is null)
            {
                int start = FirstStartingAfter(firstRow - 1);
                return new BandPlaces(null, start, Math.Max(0, FirstStartingAfter(lastRow) - start));
            }

            List<int> places = [];

            for (int row = firstRow; row <= Math.Min(lastRow, _rows.Length); row++)
                places.AddRange(_rows[row - 1]);

            places.Sort();
            return new BandPlaces(places, 0, places.Count);
        }

        private static bool InRowOrder(List<CellBlock> cells)
        {
            for (int place = 1; place < cells.Count; place++)
            {
                if (cells[place].Row < cells[place - 1].Row)
                    return false;
            }

            return true;
        }

        /// <summary>In a band in row order, the place of the first cell starting below <paramref name="row"/>.</summary>
        private int FirstStartingAfter(int row)
        {
            int low = 0;
            int high = _cells.Count;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);

                if (_cells[middle].Row <= row)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        public CellBlock this[int place] => _cells[place];

        /// <summary>Whether no other cell starts below the cell at <paramref name="place"/> in any column it covers.</summary>
        public bool IsLastInItsColumns(int place)
        {
            _lastInColumns ??= LastInTheirColumns();
            return _lastInColumns[place];
        }

        /// <summary>
        /// For every cell, whether it is the last of its columns: once the lowest start in each column is known, a
        /// cell is last when none of its columns has a cell starting below it.
        /// </summary>
        private bool[] LastInTheirColumns()
        {
            int[] lowestStart = new int[_cells.Max(cell => cell.LastColumn) + 1];

            foreach (CellBlock cell in _cells)
            {
                for (int column = cell.Column; column <= cell.LastColumn; column++)
                    lowestStart[column] = Math.Max(lowestStart[column], cell.Row);
            }

            bool[] last = new bool[_cells.Count];

            for (int place = 0; place < _cells.Count; place++)
            {
                CellBlock cell = _cells[place];
                last[place] = true;

                for (int column = cell.Column; column <= cell.LastColumn; column++)
                    last[place] &= lowestStart[column] <= cell.LastRow;
            }

            return last;
        }
    }

    /// <summary>
    /// Places in a band's list, in order: a run of it for a band in row order, which takes nothing to hold, or a list
    /// gathered from the rows' index for one that is not.
    /// </summary>
    internal readonly struct BandPlaces(List<int>? places, int start, int count)
    {
        public int Count => count;

        public int this[int index] => places is null ? start + index : places[index];

        public int[] ToArray()
        {
            int[] array = new int[count];

            for (int index = 0; index < count; index++)
                array[index] = this[index];

            return array;
        }
    }

    private int _completedRows;

    private TableLayout? _cachedLayout;

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

    internal override int ChildCount => Cells.Count + HeaderCells.Count + FooterCells.Count;

    // Body cells, then header cells, then footer cells, as GetChildren lists them.
    internal override Block? ChildAt(int index)
    {
        if (index < Cells.Count)
            return Cells[index];

        index -= Cells.Count;

        return index < HeaderCells.Count ? HeaderCells[index] : FooterCells[index - HeaderCells.Count];
    }

    protected override void ResetOwnState()
    {
        _completedRows = 0;
        _cachedLayout = null;
        _groups = null;
        _cellTags = null;
    }

    protected override object? SaveOwnProgress() => (_completedRows, _cachedLayout);

    protected override void RestoreOwnProgress(object progress) =>
        (_completedRows, _cachedLayout) = ((int, TableLayout?))progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        TableLayout? layout = BuildLayout(availableSpace, context);

        if (layout is null)
            return Fit.Defer("The table columns do not fit within the available width.");

        if (layout.Unset is { } unset)
            return Fit.Defer(unset);

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
            || layout.Unset is not null
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
                DrawBand(layout.Head, layout.HeaderHeights, layout, 1, layout.HeaderHeights.Length, top, context, head, heads: true);

            top += layout.HeaderHeight;
        }

        DrawBand(layout.Body, layout.BodyHeights, layout, _completedRows + 1, lastRow, top, context, body, heads: false, ExtendLastCells);
        top += takenHeight;

        if (layout.FooterHeights.Length != 0)
        {
            using TagStack.Scope scope = tagging && repeat ? tags.Untag() : default;
            DrawBand(layout.Foot, layout.FooterHeights, layout, 1, layout.FooterHeights.Length, top, context, foot, heads: false);
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
        BandCells cells,
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
        BandPlaces places = cells.Starting(firstRow, lastRow);

        if (group is not null)
            TagCells(CellsAt(cells, places), group, heads, context.Tags);

        for (int index = 0; index < places.Count; index++)
        {
            int place = places[index];
            CellBlock cell = cells[place];
            StructureElement? element = null;
            _cellTags?.TryGetValue(cell, out element);
            using TagStack.Scope scope = context.Tags.Enter(element);

            // The last cell of its columns reaches down to the last row drawn here.
            int bottomRow = extendLastCells && cells.IsLastInItsColumns(place) ? lastRow : cell.LastRow;

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

            context.Surface.MoveOrigin(offset);
            context.RenderAllotted(cell, cellSpace, Extent.Max.Height);
            context.Surface.MoveOrigin(offset.Reverse());
        }
    }

    private static IEnumerable<CellBlock> CellsAt(BandCells cells, BandPlaces places)
    {
        for (int index = 0; index < places.Count; index++)
            yield return cells[places[index]];
    }

    /// <summary>
    /// Creates the rows about to be drawn in <paramref name="group"/>, in order, and their cells in column order:
    /// headings of their columns in a header band, of their rows where marked, data otherwise.
    /// </summary>
    private void TagCells(IEnumerable<CellBlock> cells, StructureElement group, bool heads, TagStack tags)
    {
        _cellTags ??= [];
        using TagStack.Scope inGroup = tags.Enter(group);

        IEnumerable<IGrouping<int, CellBlock>> rows = cells
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

    /// <summary>
    /// Resolves column widths and row heights, reusing the previous result when nothing they depend on has
    /// changed.
    /// </summary>
    /// <remarks>
    /// Caching is not merely an optimisation here, it is a correctness improvement. Row heights depend only on
    /// the content and the column widths, both of which are fixed for the life of a render pass — but measuring
    /// a cell that has already been drawn plans to nothing, so recomputing on a later page would report the wrong
    /// heights for rows behind the cursor. Computing once, while every cell is still fresh, avoids that.
    ///
    /// Without it the cost is quadratic: every page re-measures every cell in the table, and the number of pages
    /// grows with the number of rows.
    /// </remarks>
    private TableLayout? BuildLayout(Extent availableSpace, PlanContext context)
    {
        ReadingDirection direction = ReadingDirection ?? context.ReadingDirection;
        float[]? columnWidths = ResolveColumnWidths(availableSpace.Width);

        if (columnWidths is null)
            return null;

        // Keyed by the columns rather than the width offered: a table of fixed columns is measured in the whole width
        // and drawn in its own when centred or on a page sized to it, and the columns, and so the rows, are the same.
        if (_cachedLayout is not null
            && _cachedLayout.Direction == direction
            && _cachedLayout.ColumnWidths.SequenceEqual(columnWidths))
        {
            // The bands are deliberately outside the cache — see MeasureBands.
            MeasureBands(_cachedLayout, context);
            return _cachedLayout;
        }

        float[] columnOffsets = new float[columnWidths.Length];

        for (int column = 1; column < columnWidths.Length; column++)
            columnOffsets[column] = columnOffsets[column - 1] + columnWidths[column - 1];

        TableLayout layout = new TableLayout
        {
            ColumnWidths = columnWidths,
            ColumnOffsets = columnOffsets,
            TotalWidth = columnWidths.Sum(),
            Direction = direction,
            Head = new BandCells(HeaderCells),
            Body = new BandCells(Cells),
            Foot = new BandCells(FooterCells)
        };

        layout.BodyHeights = MeasureRowHeights(Cells, layout, context, "body", out string? bodyUnset);
        layout.BodyUnset = bodyUnset;
        layout.GroupEnd = BuildGroupBoundaries(Cells, layout.BodyHeights.Length);

        _cachedLayout = layout;

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
        layout.HeaderHeights = MeasureRowHeights(HeaderCells, layout, context, "header", out string? headerUnset);
        layout.FooterHeights = MeasureRowHeights(FooterCells, layout, context, "footer", out string? footerUnset);
        layout.BandUnset = headerUnset ?? footerUnset;
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
    /// the rows they cover. <paramref name="unset"/> says why a cell cannot be set in the rows it is given, if one
    /// cannot.
    /// </summary>
    private static float[] MeasureRowHeights(List<CellBlock> cells, TableLayout layout, PlanContext context, string band, out string? unset)
    {
        unset = null;

        if (cells.Count == 0)
            return [];

        int rowCount = cells.Max(cell => cell.LastRow);
        float[] heights = new float[rowCount];
        List<CellBlock>? unmeasured = null;

        // Unspanned cells set the baseline height of the row they sit in.
        foreach (CellBlock cell in cells.Where(cell => cell.RowSpan <= 1))
        {
            Fit plan = cell.Plan(new Extent(layout.SpanWidth(cell), Extent.Max.Height), context);

            if (plan.IsDeferred)
                (unmeasured ??= []).Add(cell);
            else
                heights[cell.Row - 1] = Math.Max(heights[cell.Row - 1], plan.Size.Height);
        }

        // A spanned cell only forces growth when the rows it covers cannot already hold it, and the shortfall
        // goes on its *last* row: charging an earlier one would push the rows below it down inside the span.
        foreach (CellBlock cell in cells.Where(cell => cell.RowSpan > 1))
        {
            Fit plan = cell.Plan(new Extent(layout.SpanWidth(cell), Extent.Max.Height), context);

            if (plan.IsDeferred)
            {
                (unmeasured ??= []).Add(cell);
                continue;
            }

            float spannedHeight = SpannedHeight(cell, heights);

            if (plan.Size.Height > spannedHeight)
                heights[cell.LastRow - 1] += plan.Size.Height - spannedHeight;
        }

        // A cell that cannot be measured in unlimited height takes no part in sizing its row — content sized by the
        // height it is given, such as an image fitted to it — but it must fit the row the others make, or it would be
        // drawn in a row that cannot hold it and lost without a word.
        foreach (CellBlock cell in unmeasured ?? [])
        {
            Extent space = new Extent(layout.SpanWidth(cell), SpannedHeight(cell, heights));
            Fit plan = cell.Plan(space, context);

            if (plan.IsDeferred)
            {
                unset = $"The cell at row {cell.Row}, column {cell.Column} of the {band} cannot be set in the {space} its row gives it. Reason: {plan.DeferReason}";
                break;
            }
        }

        return heights;
    }

    /// <summary>The combined height of the rows <paramref name="cell"/> covers.</summary>
    private static float SpannedHeight(CellBlock cell, float[] heights)
    {
        float height = 0f;

        for (int row = cell.Row; row <= cell.LastRow; row++)
            height += heights[row - 1];

        return height;
    }

    /// <summary>
    /// Splits the available width across columns: fixed columns keep their width, and share columns divide the rest.
    /// </summary>
    /// <remarks>Null means the table cannot be laid out at this width, which the caller turns into a Defer.</remarks>
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
