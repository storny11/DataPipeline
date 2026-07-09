// Represents a warning or error produced by a step for one identified record or operation.
namespace DataRetriever.Execution;

public sealed record StepIssue(
    string StepName,
    string IdentifierName,
    string IdentifierValue,
    StepIssueSeverity Severity,
    string Message);
