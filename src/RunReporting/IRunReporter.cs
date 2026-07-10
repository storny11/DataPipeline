// One scoped collector per run: contributors add report data at origin, then the
// orchestration boundary completes and publishes one immutable snapshot.
namespace RunReporting;

public interface IRunReporter
{
    /// <summary>Adds or overwrites a run attribute; shown in the report header.</summary>
    void AddAttribute(string name, string? value);

    void AddIssue(string message, IssueSeverity severity = IssueSeverity.Warning);

    /// <summary>Reports an issue for one identified record or operation within a step.</summary>
    void AddIssue(
        string stepName,
        string identifierName,
        string identifierValue,
        string message,
        IssueSeverity severity = IssueSeverity.Warning);

    /// <summary>Adds a result set to the published report. Every column explicitly defines its displayed
    /// header, row value selector, alignment, and optional format string.</summary>
    void AddTable<TRow>(
        string title,
        IEnumerable<TRow> rows,
        IReadOnlyList<TableColumn<TRow>> columns);

    /// <summary>Completes this scoped run, publishes its snapshot once, and returns that snapshot.
    /// Repeated calls return the same report without publishing again.</summary>
    Task<RunReport> CompleteAsync(RunOutcome? outcome = null);
}
