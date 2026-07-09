// Represents a single issue reported against a run. StepName + Key is its removable
// identity; Data is display-only diagnostic context.
namespace RunReporting;

public sealed record RunIssue(
    string StepName,
    string Key,
    IssueSeverity Severity,
    string Message,
    IReadOnlyDictionary<string, string?> Data,
    DateTimeOffset TimestampUtc)
{
    internal static readonly IReadOnlyDictionary<string, string?> EmptyData =
        new Dictionary<string, string?>();

    public static RunIssue Create(
        string? stepName,
        string? key,
        IssueSeverity severity,
        string? message,
        IReadOnlyDictionary<string, string?>? data = null)
    {
        return new RunIssue(
            stepName ?? string.Empty,
            key ?? string.Empty,
            severity,
            message ?? string.Empty,
            data ?? EmptyData,
            DateTimeOffset.UtcNow);
    }
}
