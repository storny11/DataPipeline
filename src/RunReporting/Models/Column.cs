// Per-column spec for result tables: alignment plus an optional .NET format string
// applied to IFormattable values ("N2", "P1", "yyyy-MM-dd"), invariant culture.
namespace RunReporting;

public sealed record Column(
    ColumnAlignment Alignment = ColumnAlignment.Left,
    string? Format = null)
{
    public static readonly Column Left = new();

    public static readonly Column Right = new(ColumnAlignment.Right);

    public static readonly Column Center = new(ColumnAlignment.Center);

    /// <summary>A formatted numeric column; right-aligned by convention.</summary>
    public static Column Number(string format = "N2") => new(ColumnAlignment.Right, format);
}
