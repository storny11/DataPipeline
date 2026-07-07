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
        string message,
        IReadOnlyDictionary<string, string?>? data = null)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        return new RunIssue(stepName ?? string.Empty, severity, message, data ?? EmptyData, DateTimeOffset.UtcNow);
    }
}
