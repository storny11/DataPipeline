using DataRetriever.Execution;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;

namespace DataRetriever.Application.Runs;

public sealed class RunReportFinalizer(
    RunInstrumentationWriter instrumentationWriter,
    RunReportBuilder reportBuilder,
    IRunReportPublisher reportPublisher)
{
    public async Task<RunReport> FinishAsync(
        RunContext context,
        DataRetrievalRunOptions options,
        IReadOnlyList<IStepExecutionResult> results,
        IReadOnlyList<PersistedRecordSummary> persistedRecords,
        RunStatus status,
        IRunInstrumentation instrumentation,
        CancellationToken cancellationToken)
    {
        instrumentationWriter.RecordRunStatus(instrumentation, status);

        var report = reportBuilder.Build(
            context,
            DateTimeOffset.UtcNow,
            status,
            new RunRequestSummary(options.Currency, options.InternalIds),
            results,
            persistedRecords);

        await reportPublisher.PublishAsync(report, cancellationToken);
        return report;
    }
}
