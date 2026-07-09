// Snapshot of everything collected for a run — attributes, outcome, issues, result tables.
namespace RunReporting;

public sealed record RunReport(
    IReadOnlyDictionary<string, string?> Attributes,
    RunOutcome Outcome,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<RunIssue> Issues,
    IReadOnlyList<ResultTable> Tables)
{
    public int ErrorCount => Issues.Count(issue => issue.Severity == IssueSeverity.Error);

    public int WarningCount => Issues.Count(issue => issue.Severity == IssueSeverity.Warning);

    public bool HasIssues => Issues.Count > 0;

    internal static RunOutcome DeriveOutcome(IReadOnlyList<RunIssue> issues)
    {
        if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
        {
            return RunOutcome.Failed;
        }

        return issues.Count > 0
            ? RunOutcome.CompletedWithWarnings
            : RunOutcome.Succeeded;
    }
}
