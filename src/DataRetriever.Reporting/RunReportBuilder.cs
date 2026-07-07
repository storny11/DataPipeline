// Aggregates step results and collected run data into the structured API report.
using DataRetriever.Execution;

namespace DataRetriever.Reporting;

public sealed class RunReportBuilder
{
    public DataRetrievalReport Build(
        RunContext context,
        DateTimeOffset completedAt,
        RunStatus status,
        IReadOnlyList<IStepExecutionResult> stepResults,
        IReadOnlyList<RunReportTable> tables,
        IReadOnlyList<RunReportIssue> issues)
    {
        // Per-step counts come from the collected issues so issues reported at origin
        // are attributed to their step.
        var steps = stepResults
            .Select(result => new RunReportStep(
                result.StepName,
                result.Status,
                result.Counters,
                issues.Count(issue => issue.StepName == result.StepName && issue.Severity == StepIssueSeverity.Warning),
                issues.Count(issue => issue.StepName == result.StepName && issue.Severity == StepIssueSeverity.Error)))
            .ToList();

        return new DataRetrievalReport(
            context.RunId,
            context.StartedAt,
            completedAt,
            status,
            steps,
            issues,
            tables);
    }
}
