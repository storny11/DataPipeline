// Represents a warning or error produced by a step with diagnostic context.
namespace DataRetriever.Execution;

public sealed record StepIssue(
    string StepName,
    StepIssueSeverity Severity,
    string Message,
    DiagnosticContext Context);
