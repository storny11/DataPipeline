// Represents a keyed warning or error produced by a step with diagnostic context.
namespace DataRetriever.Execution;

public sealed record StepIssue(
    string StepName,
    string Key,
    StepIssueSeverity Severity,
    string Message,
    DiagnosticContext Context);
