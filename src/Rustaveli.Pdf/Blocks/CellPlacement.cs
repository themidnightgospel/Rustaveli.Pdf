namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Fills a grid left to right, top to bottom, skipping slots already claimed by explicitly positioned or
/// spanning cells.
/// </summary>
internal static class CellPlacement
{
    /// <exception cref="CompositionException">A cell pinned to a row finds no room left in it.</exception>
    public static void Apply(List<CellBlock> cells, int columnCount, string band)
    {
        HashSet<(int Row, int Column)> occupied = new HashSet<(int Row, int Column)>();

        // Fully pinned cells claim their slots first, so everything placed afterwards can avoid them.
        foreach (CellBlock? cell in cells.Where(cell => cell is { HasExplicitRow: true, HasExplicitColumn: true }))
            Occupy(occupied, cell);

        // A cell that pins only one axis still needs the other resolved. Treating it as fully placed would
        // leave the unpinned axis at its default of 1, silently stacking such cells on top of one another.
        foreach (CellBlock? cell in cells.Where(cell => cell.HasExplicitRow && !cell.HasExplicitColumn))
        {
            int span = Math.Min(Math.Max(1, cell.ColumnSpan), columnCount);
            int candidate = 1;

            while (candidate + span - 1 <= columnCount && !IsFree(occupied, cell.Row, candidate, span, cell.RowSpan))
                candidate++;

            // Moved to another row it would not be where it was put, and kept in this one it would cover a cell that is.
            if (candidate + span - 1 > columnCount)
            {
                throw new CompositionException(
                    $"A {band} cell is placed in row {cell.Row}, but the row has no room left for its {span}-column span. Give the cell a column, or place it in another row.");
            }

            cell.Column = candidate;
            Occupy(occupied, cell);
        }

        foreach (CellBlock? cell in cells.Where(cell => cell.HasExplicitColumn && !cell.HasExplicitRow))
        {
            int candidate = 1;

            while (!IsFree(occupied, candidate, cell.Column, Math.Max(1, cell.ColumnSpan), cell.RowSpan))
                candidate++;

            cell.Row = candidate;
            Occupy(occupied, cell);
        }

        int row = 1;
        int column = 1;

        foreach (CellBlock? cell in cells.Where(cell => !cell.HasExplicitRow && !cell.HasExplicitColumn))
        {
            int span = Math.Min(Math.Max(1, cell.ColumnSpan), columnCount);

            while (!IsFree(occupied, row, column, span, cell.RowSpan) || column + span - 1 > columnCount)
            {
                column++;

                if (column > columnCount)
                {
                    column = 1;
                    row++;
                }
            }

            cell.Row = row;
            cell.Column = column;
            Occupy(occupied, cell);

            column += span;

            if (column > columnCount)
            {
                column = 1;
                row++;
            }
        }
    }

    private static bool IsFree(HashSet<(int, int)> occupied, int row, int column, int columnSpan, int rowSpan)
    {
        for (int r = row; r < row + Math.Max(1, rowSpan); r++)
        {
            for (int c = column; c < column + columnSpan; c++)
            {
                if (occupied.Contains((r, c)))
                    return false;
            }
        }

        return true;
    }

    private static void Occupy(HashSet<(int Row, int Column)> occupied, CellBlock cell)
    {
        for (int row = cell.Row; row <= cell.LastRow; row++)
        {
            for (int column = cell.Column; column <= cell.LastColumn; column++)
                occupied.Add((row, column));
        }
    }
}
