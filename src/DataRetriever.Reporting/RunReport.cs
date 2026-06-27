using DataRetriever.Execution;

namespace DataRetriever.Reporting;

public sealed record RunReport(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    RunStatus Status,
    RunRequestSummary Request,
    RunReportSummary Summary,
    IReadOnlyList<RunReportStep> Steps,
    IReadOnlyList<RunReportIssue> Issues,
    IReadOnlyList<PersistedRecordSummary> PersistedRecords);

public sealed record RunReportSummary(
    long ConfiguredRowsReturned,
    long RowsAfterFiltering,
    long Step2RowsProduced,
    long ValidStep3RowsReturned,
    long RowsPersisted,
    int WarningCount,
    int ErrorCount);

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
