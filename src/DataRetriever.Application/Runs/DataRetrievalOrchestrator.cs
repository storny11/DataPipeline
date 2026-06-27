using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;
using Microsoft.Extensions.Logging;

namespace DataRetriever.Application.Runs;

public sealed class DataRetrievalOrchestrator(
    IStep<Step1Input, Step1Output> step1,
    IStep<Step1Output, Step2Output> step2,
    IStep<Step2Output, Step3Output> step3,
    IStep<Step3Output, Step4Output> step4,
    IProcessingTracker processingTracker,
    StepRunner stepRunner,
    PersistedRecordSummaryMapper persistedRecordSummaryMapper,
    RunInstrumentationWriter instrumentationWriter,
    RunReportFinalizer reportFinalizer,
    ILogger<DataRetrievalOrchestrator> logger)
{
    public async Task<RunReport> RunAsync(
        DataRetrievalRunOptions options,
        CancellationToken cancellationToken)
    {
        var context = new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var instrumentation = processingTracker.ForRun(context.RunId);
        instrumentationWriter.RecordRunStatus(instrumentation, RunStatus.Running);

        var results = new List<IStepExecutionResult>();
        IReadOnlyList<PersistedRecordSummary> persistedRecords = [];

        try
        {
            var step1Result = await stepRunner.ExecuteAsync(
                step1,
                new Step1Input(options),
                context,
                instrumentation,
                results,
                cancellationToken);

            if (!CanContinue(step1Result))
            {
                return await FinishFailedAsync(context, options, results, persistedRecords, instrumentation, cancellationToken);
            }

            var step2Result = await stepRunner.ExecuteAsync(
                step2,
                step1Result.Output!,
                context,
                instrumentation,
                results,
                cancellationToken);

            if (!CanContinue(step2Result))
            {
                return await FinishFailedAsync(context, options, results, persistedRecords, instrumentation, cancellationToken);
            }

            var step3Result = await stepRunner.ExecuteAsync(
                step3,
                step2Result.Output!,
                context,
                instrumentation,
                results,
                cancellationToken);

            if (!CanContinue(step3Result))
            {
                return await FinishFailedAsync(context, options, results, persistedRecords, instrumentation, cancellationToken);
            }

            var step4Result = await stepRunner.ExecuteAsync(
                step4,
                step3Result.Output!,
                context,
                instrumentation,
                results,
                cancellationToken);

            var finalStatus = CanContinue(step4Result) ? RunStatus.Success : RunStatus.Failed;
            persistedRecords = persistedRecordSummaryMapper.Map(step4Result.Output);

            return await reportFinalizer.FinishAsync(
                context,
                options,
                results,
                persistedRecords,
                finalStatus,
                instrumentation,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unexpected data retrieval run failure for {RunId}", context.RunId);
            results.Add(StepExecutionResult<NoOutput>.Failed(
                "Run",
                [
                    new StepIssue(
                        "Run",
                        StepIssueSeverity.Error,
                        $"Unexpected run failure: {exception.Message}",
                        DiagnosticContext.From(("runId", context.RunId.ToString())))
                ]));

            return await FinishFailedAsync(context, options, results, persistedRecords, instrumentation, cancellationToken);
        }
    }

    private Task<RunReport> FinishFailedAsync(
        RunContext context,
        DataRetrievalRunOptions options,
        IReadOnlyList<IStepExecutionResult> results,
        IReadOnlyList<PersistedRecordSummary> persistedRecords,
        IRunInstrumentation instrumentation,
        CancellationToken cancellationToken)
    {
        return reportFinalizer.FinishAsync(
            context,
            options,
            results,
            persistedRecords,
            RunStatus.Failed,
            instrumentation,
            cancellationToken);
    }

    private static bool CanContinue<TOutput>(StepExecutionResult<TOutput> result)
    {
        return !result.HasErrors && result.HasUsableOutput;
    }
}
