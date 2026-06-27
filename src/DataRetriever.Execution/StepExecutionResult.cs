// Wraps a step output together with status, counters, and issues.
namespace DataRetriever.Execution;

public sealed record StepExecutionResult<TOutput>(
    string StepName,
    StepExecutionStatus Status,
    TOutput? Output,
    bool HasUsableOutput,
    IReadOnlyList<StepCounter> Counters,
    IReadOnlyList<StepIssue> Issues) : IStepExecutionResult
{
    public bool HasIssues => Issues.Count > 0;

    public bool HasErrors => Issues.Any(issue => issue.Severity == StepIssueSeverity.Error);

    public static StepExecutionResult<TOutput> Success(
        string stepName,
        TOutput output,
        IEnumerable<StepCounter>? counters = null,
        IEnumerable<StepIssue>? issues = null)
    {
        var issueList = (issues ?? []).ToList();
        var status = ResolveStatus(issueList);
        return new StepExecutionResult<TOutput>(
            stepName,
            status,
            output,
            status != StepExecutionStatus.Failed,
            (counters ?? []).ToList(),
            issueList);
    }

    public static StepExecutionResult<TOutput> Failed(
        string stepName,
        IEnumerable<StepIssue> issues,
        IEnumerable<StepCounter>? counters = null)
    {
        return new StepExecutionResult<TOutput>(
            stepName,
            StepExecutionStatus.Failed,
            default,
            false,
            (counters ?? []).ToList(),
            issues.ToList());
    }

    public static StepExecutionResult<NoOutput> Completed(
        string stepName,
        IEnumerable<StepCounter>? counters = null,
        IEnumerable<StepIssue>? issues = null)
    {
        var issueList = (issues ?? []).ToList();
        var status = ResolveStatus(issueList);
        return new StepExecutionResult<NoOutput>(
            stepName,
            status,
            NoOutput.Value,
            status != StepExecutionStatus.Failed,
            (counters ?? []).ToList(),
            issueList);
    }

    private static StepExecutionStatus ResolveStatus(IReadOnlyCollection<StepIssue> issues)
    {
        if (issues.Any(issue => issue.Severity == StepIssueSeverity.Error))
        {
            return StepExecutionStatus.Failed;
        }

        return issues.Count == 0
            ? StepExecutionStatus.Succeeded
            : StepExecutionStatus.SucceededWithIssues;
    }
}
