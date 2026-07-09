// Explicit definition of one result-table column: its heading, value selector,
// alignment, and optional invariant-culture format string.
namespace RunReporting;

public sealed record TableColumn<TRow>(
    string Header,
    Func<TRow, object?> ValueSelector,
    ColumnAlignment Alignment = ColumnAlignment.Left,
    string? Format = null)
{
    public static TableColumn<TRow> Left(string header, Func<TRow, object?> valueSelector) =>
        new(header, valueSelector);

    public static TableColumn<TRow> Right(string header, Func<TRow, object?> valueSelector) =>
        new(header, valueSelector, ColumnAlignment.Right);

    public static TableColumn<TRow> Center(string header, Func<TRow, object?> valueSelector) =>
        new(header, valueSelector, ColumnAlignment.Center);

    /// <summary>A formatted numeric column; right-aligned by convention.</summary>
    public static TableColumn<TRow> Number(
        string header,
        Func<TRow, object?> valueSelector,
        string format = "N2") =>
        new(header, valueSelector, ColumnAlignment.Right, format);
}
