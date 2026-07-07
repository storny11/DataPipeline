// Represents a single issue reported against a run, with optional step name and diagnostic data.
namespace RunReporting;

public sealed record RunIssue(
    string StepName,
    IssueSeverity Severity,
    string Message,
    IReadOnlyDictionary<string, string?> Data,
    DateTimeOffset TimestampUtc)
{
    internal static readonly IReadOnlyDictionary<string, string?> EmptyData =
        new Dictionary<string, string?>();

    public static RunIssue Create(
        string? stepName,
        IssueSeverity severity,
        string? message,
        IReadOnlyDictionary<string, string?>? data = null)
    {
        return new RunIssue(
            stepName ?? string.Empty,
            severity,
            message ?? string.Empty,
            data ?? EmptyData,
            DateTimeOffset.UtcNow);
    }
}
