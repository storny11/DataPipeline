using System.Globalization;
using DataRetriever.Execution;
using DataRetriever.Reporting;

namespace DataRetriever.Application.Runs;

public sealed class DataRetrievalReportSummaryBuilder
{
    public IReadOnlyList<RunReportMetric> Build(IReadOnlyList<IStepExecutionResult> stepResults)
    {
        return
        [
            Metric("configuredRowsReturned", "Configured rows", Counter(stepResults, "ConfiguredRowsReturned")),
            Metric("rowsAfterFiltering", "Rows after filtering", Counter(stepResults, "RowsAfterFiltering")),
            Metric("step2RowsProduced", "Step 2 rows", Counter(stepResults, "Step2RowsProduced")),
            Metric("validStep3RowsReturned", "Step 3 valid rows", Counter(stepResults, "ValidStep3RowsReturned")),
            Metric("rowsPersisted", "Rows persisted", Counter(stepResults, "RowsSuccessfullyPersisted"))
        ];
    }

    private static RunReportMetric Metric(string name, string label, long value)
    {
        return new RunReportMetric(name, label, value.ToString(CultureInfo.InvariantCulture));
    }

    private static long Counter(IEnumerable<IStepExecutionResult> stepResults, string name)
    {
        return stepResults
            .SelectMany(result => result.Counters)
            .Where(counter => string.Equals(counter.Name, name, StringComparison.OrdinalIgnoreCase))
            .Sum(counter => counter.Value);
    }
}
