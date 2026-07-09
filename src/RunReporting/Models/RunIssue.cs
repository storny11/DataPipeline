// Represents a single issue reported against a run. The identifier names the record or
// operation involved without introducing an open-ended diagnostic-data shape.
namespace RunReporting;

public sealed record RunIssue(
    string StepName,
    string IdentifierName,
    string IdentifierValue,
    IssueSeverity Severity,
    string Message,
    DateTimeOffset TimestampUtc)
{
    internal static RunIssue Create(
        string? stepName,
        string? identifierName,
        string? identifierValue,
        IssueSeverity severity,
        string? message)
    {
        return new RunIssue(
            stepName ?? string.Empty,
            identifierName ?? string.Empty,
            identifierValue ?? string.Empty,
            severity,
            message ?? string.Empty,
            DateTimeOffset.UtcNow);
    }
}
