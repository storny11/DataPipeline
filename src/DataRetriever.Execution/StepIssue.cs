namespace DataRetriever.Execution;

public sealed record StepIssue(
    string StepName,
    StepIssueSeverity Severity,
    string Message,
    DiagnosticContext Context);
