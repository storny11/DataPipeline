// A published result set containing the explicit headings, alignments, and formatted
// cell values selected by the caller's table-column definitions.

namespace RunReporting;

public sealed record ResultTable(
    string Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<ColumnAlignment> Alignments,
    IReadOnlyList<IReadOnlyList<string?>> Rows)
{
    public static ResultTable From<TRow>(
        string? title,
        IEnumerable<TRow>? rows,
        IReadOnlyList<TableColumn<TRow>>? columns)
    {
        var safeColumns = (columns ?? [])
            .Where(column => column != null)
            .ToList();

        return new ResultTable(
            title ?? string.Empty,
            safeColumns.Select(column => column.Header ?? string.Empty).ToList(),
            safeColumns.Select(column => column.Alignment).ToList(),
            (rows ?? []).Select(row => ToRow(row, safeColumns)).ToList());
    }

    private static IReadOnlyList<string?> ToRow<TRow>(
        TRow row,
        IReadOnlyList<TableColumn<TRow>> columns)
    {
        return columns
            .Select(column => FormatCell(column, row))
            .ToList();
    }

    private static string? FormatCell<TRow>(TableColumn<TRow> column, TRow row)
    {
        try
        {
            return ValueFormatter.Format(column.ValueSelector(row), column.Format);
        }
        catch
        {
            // A bad selector costs one cell, never the table or caller.
            return null;
        }
    }
}
