// Coordinates the concrete step sequence and returns the final run report.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;
using Microsoft.Extensions.Logging;
using RunReporting;

namespace DataRetriever.Application.Runs;

public sealed class DataRetrievalOrchestrator(
    IStep<Step1Input, Step1Output> step1,
    IStep<Step1Output, Step2Output> step2,
    IStep<Step2Output, Step3Output> step3,
    IStep<Step3Output, Step4Output> step4,
    IProcessingTracker processingTracker,
    StepRunner stepRunner,
    RunInstrumentationWriter instrumentationWriter,
    IRunReporter runReporter,
    RunReportBuilder reportBuilder,
    ILogger<DataRetrievalOrchestrator> logger)
{
    public async Task<DataRetrievalReport> RunAsync(
        DataRetrievalRunOptions options,
        CancellationToken cancellationToken)
    {
        var context = new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow);
        using var run = runReporter.BeginRun(("runId", context.RunId.ToString()));
        var instrumentation = processingTracker.ForRun(context.RunId);
        instrumentationWriter.RecordRunStatus(instrumentation, RunStatus.Running);

        var results = new List<IStepExecutionResult>();
        RunStatus status;

        try
        {
            status = await ExecuteStepsAsync(options, context, instrumentation, results, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unexpected data retrieval run failure for {RunId}", context.RunId);
            var runFailure = StepExecutionResult<NoOutput>.Failed(
                "Run",
                [
                    new StepIssue(
                        "Run",
                        StepIssueSeverity.Error,
                        $"Unexpected run failure: {exception.Message}",
                        DiagnosticContext.From(("runId", context.RunId.ToString())))
                ]);

            results.Add(runFailure);
            runReporter.AddIssue(
                new { runId = context.RunId.ToString() },
                $"Unexpected run failure: {exception.Message}",
                "Run",
                IssueSeverity.Error);
            status = RunStatus.Failed;

            try
            {
                instrumentationWriter.RecordStepResult(instrumentation, runFailure);
            }
            catch (Exception instrumentationException)
            {
                logger.LogError(
                    instrumentationException,
                    "Failed to record instrumentation step result for failed run {RunId}",
                    context.RunId);
            }
        }

        // Finish exactly once: publish drains and delivers everything reported at origin,
        // and the API response is shaped from the same published report.
        instrumentationWriter.RecordRunStatus(instrumentation, status);
        var published = await runReporter.PublishAsync(
            status == RunStatus.Failed ? RunOutcome.Failed : null,
            cancellationToken);

        return reportBuilder.Build(
            context,
            DateTimeOffset.UtcNow,
            status,
            results,
            published.Tables.Select(RunReportMapper.ToRunReportTable).ToList(),
            published.Issues.Select(RunReportMapper.ToRunReportIssue).ToList());
    }

    private async Task<RunStatus> ExecuteStepsAsync(
        DataRetrievalRunOptions options,
        RunContext context,
        IRunInstrumentation instrumentation,
        List<IStepExecutionResult> results,
        CancellationToken cancellationToken)
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
            return RunStatus.Failed;
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
            return RunStatus.Failed;
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
            return RunStatus.Failed;
        }

        var step4Result = await stepRunner.ExecuteAsync(
            step4,
            step3Result.Output!,
            context,
            instrumentation,
            results,
            cancellationToken);

        return CanContinue(step4Result) ? RunStatus.Success : RunStatus.Failed;
    }

    private static bool CanContinue<TOutput>(StepExecutionResult<TOutput> result)
    {
        return !result.HasErrors && result.HasUsableOutput;
    }
}
