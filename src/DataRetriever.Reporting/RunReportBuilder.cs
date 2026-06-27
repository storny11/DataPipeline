using DataRetriever.Execution;

namespace DataRetriever.Reporting;

public sealed class RunReportBuilder
{
    public RunReport Build(
        RunContext context,
        DateTimeOffset completedAt,
        RunStatus status,
        RunRequestSummary request,
        IReadOnlyList<IStepExecutionResult> stepResults,
        IReadOnlyList<RunReportMetric> summary,
        IReadOnlyList<RunReportTable> tables)
    {
        var issues = stepResults
            .SelectMany(result => result.Issues)
            .Select(issue => new RunReportIssue(issue.StepName, issue.Severity, issue.Message, issue.Context))
            .ToList();

        var steps = stepResults
            .Select(result => new RunReportStep(
                result.StepName,
                result.Status,
                result.Counters,
                result.Issues.Count(issue => issue.Severity == StepIssueSeverity.Warning),
                result.Issues.Count(issue => issue.Severity == StepIssueSeverity.Error)))
            .ToList();

        return new RunReport(
            context.RunId,
            context.StartedAt,
            completedAt,
            status,
            request,
            summary,
            steps,
            issues,
            tables);
    }
}
