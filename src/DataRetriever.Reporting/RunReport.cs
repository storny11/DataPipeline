// Defines the structured report model shared by API responses and email formatting.
using DataRetriever.Execution;

namespace DataRetriever.Reporting;

public sealed record RunReport(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    RunStatus Status,
    RunRequestSummary Request,
    IReadOnlyList<RunReportMetric> Summary,
    IReadOnlyList<RunReportStep> Steps,
    IReadOnlyList<RunReportIssue> Issues,
    IReadOnlyList<RunReportTable> Tables)
{
    public int WarningCount => Issues.Count(issue => issue.Severity == StepIssueSeverity.Warning);

    public int ErrorCount => Issues.Count(issue => issue.Severity == StepIssueSeverity.Error);
}

public sealed record RunReportMetric(
    string Name,
    string Label,
    string Value);

public sealed record RunReportStep(
    string StepName,
    StepExecutionStatus Status,
    IReadOnlyList<StepCounter> Counters,
    int WarningCount,
    int ErrorCount);

public sealed record RunReportIssue(
    string StepName,
    StepIssueSeverity Severity,
    string Message,
    DiagnosticContext Context);

public sealed record RunRequestSummary(
    string? Currency,
    IReadOnlyList<string> InternalIds);

public sealed record RunReportTable(
    string Name,
    string Title,
    IReadOnlyList<RunReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

public sealed record RunReportColumn(
    string Key,
    string Header,
    RunReportColumnAlignment Alignment = RunReportColumnAlignment.Left);

public enum RunReportColumnAlignment
{
    Left,
    Right,
    Center
}
