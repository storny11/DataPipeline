// The one interface, injectable like ILogger: collect issues and result tables against the
// ambient run started by BeginRun (or a default process-wide run), then publish the report.
namespace RunReporting;

public interface IRunReporter
{
    /// <summary>Starts a run for the current async flow, like ILogger.BeginScope. Attributes (environment, run id, ...) are shown in the report. Dispose to restore the previous run; publish before disposing or the collected data is discarded.</summary>
    IDisposable BeginRun(IReadOnlyDictionary<string, string?>? attributes = null);

    /// <summary>Convenience overload: BeginRun(("runId", id), ("environment", "prod")).</summary>
    IDisposable BeginRun(params (string Name, string? Value)[] attributes);

    /// <summary>Adds or overwrites a run attribute at any point during the run; shown in the report header.</summary>
    void AddAttribute(string name, string? value);

    void AddIssue(string message, IssueSeverity severity = IssueSeverity.Warning);

    /// <summary>Reports an issue about a subject: a scalar id, a list of ids, a dictionary, or an object like new { ccy, id }.</summary>
    void AddIssue(object? subject, string message, string? stepName = null, IssueSeverity severity = IssueSeverity.Warning);

    /// <summary>Adds a result set to the published report. Rows are plain or anonymous objects; columns come
    /// from the first row's properties with headers like "INTERNAL ID". Project into anonymous objects to
    /// choose or rename columns. Columns align left unless an alignment is given per column.</summary>
    void AddTable(string title, IEnumerable<object?> rows, IReadOnlyList<ColumnAlignment>? alignments = null);

    /// <summary>Removes and returns everything collected for the current run, without sending anything. The outcome is derived from the issues.</summary>
    RunReport Take();

    /// <summary>Removes everything collected for the current run, sends the report, and returns it; pass an outcome to override the derived one.</summary>
    Task<RunReport> PublishAsync(RunOutcome? outcome = null, CancellationToken cancellationToken = default);

    /// <summary>Sends an already-composed report through the configured sender.</summary>
    Task PublishAsync(RunReport report, CancellationToken cancellationToken = default);
}
