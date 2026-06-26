using DataRetriever.Execution;

namespace DataRetriever.Reporting;

public sealed class RunReportBuilder
{
    public RunReport Build(
        RunContext context,
        DateTimeOffset completedAt,
        string status,
        RunRequestSummary request,
        IReadOnlyList<IStepExecutionResult> stepResults,
        IReadOnlyList<PersistedRecordSummary> persistedRecords)
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

        var summary = new RunReportSummary(
            Counter(stepResults, "ConfiguredRowsReturned"),
            Counter(stepResults, "RowsAfterFiltering"),
            Counter(stepResults, "Step2RowsProduced"),
            Counter(stepResults, "ValidStep3RowsReturned"),
            Counter(stepResults, "RowsSuccessfullyPersisted"),
            issues.Count(issue => issue.Severity == StepIssueSeverity.Warning),
            issues.Count(issue => issue.Severity == StepIssueSeverity.Error));

        return new RunReport(
            context.RunId,
            context.StartedAt,
            completedAt,
            status,
            request,
            summary,
            steps,
            issues,
            persistedRecords);
    }

    private static long Counter(IEnumerable<IStepExecutionResult> stepResults, string name)
    {
        return stepResults
            .SelectMany(result => result.Counters)
            .Where(counter => string.Equals(counter.Name, name, StringComparison.OrdinalIgnoreCase))
            .Sum(counter => counter.Value);
    }
}
