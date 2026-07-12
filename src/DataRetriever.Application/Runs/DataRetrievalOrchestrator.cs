// Coordinates the concrete step sequence and publishes the run report on every exit path,
// including cancellation and unexpected failure.
using System.Globalization;
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using Microsoft.Extensions.Hosting;
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
    ILogger<DataRetrievalOrchestrator> logger,
    IHostEnvironment? hostEnvironment = null)
{
    public async Task<DataRetrievalRunResult> RunAsync(
        DataRetrievalRunOptions options,
        CancellationToken cancellationToken)
    {
        var context = new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow);
        runReporter.AddAttribute("runId", context.RunId.ToString());
        runReporter.AddAttribute($"started ({TimeLabel})", FormatRunTimestamp(context.StartedAt));
        if (hostEnvironment != null)
        {
            runReporter.AddAttribute("environment", hostEnvironment.EnvironmentName);
        }

        var instrumentation = processingTracker.ForRun(context.RunId);
        instrumentationWriter.RecordRunStatus(instrumentation, RunStatus.Running);

        RunStatus status;
        try
        {
            status = await ExecuteStepsAsync(options, context, instrumentation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A cancelled run must still publish whatever it collected: data persisted by earlier
            // steps is already visible to users, and the tracker must not stay on Running forever.
            RecordRunInterruption(
                context,
                instrumentation,
                "Run was cancelled before completion; the report may be incomplete.");
            await FinishRunAsync(instrumentation, RunStatus.Cancelled);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected data retrieval run failure for {RunId}", context.RunId);
            RecordRunInterruption(
                context,
                instrumentation,
                $"Unexpected run failure: {exception.Message}");
            status = RunStatus.Failed;
        }

        await FinishRunAsync(instrumentation, status);
        return new DataRetrievalRunResult(context.RunId, status);
    }

    private async Task FinishRunAsync(IRunInstrumentation instrumentation, RunStatus status)
    {
        instrumentationWriter.RecordRunStatus(instrumentation, status);
        runReporter.AddAttribute($"completed ({TimeLabel})", FormatRunTimestamp(DateTimeOffset.UtcNow));
        await runReporter.CompleteAsync(status == RunStatus.Success ? null : RunOutcome.Failed);
    }

    private void RecordRunInterruption(
        RunContext context,
        IRunInstrumentation instrumentation,
        string message)
    {
        runReporter.AddIssue("Run", "RunId", context.RunId.ToString(), message, IssueSeverity.Error);

        try
        {
            instrumentationWriter.RecordStepResult(
                instrumentation,
                StepExecutionResult<NoOutput>.Failed(
                    "Run",
                    [
                        new StepIssue(
                            "Run",
                            "RunId",
                            context.RunId.ToString(),
                            StepIssueSeverity.Error,
                            message)
                    ]));
        }
        catch (Exception instrumentationException)
        {
            logger.LogError(
                instrumentationException,
                "Failed to record instrumentation step result for interrupted run {RunId}",
                context.RunId);
        }
    }

    private async Task<RunStatus> ExecuteStepsAsync(
        DataRetrievalRunOptions options,
        RunContext context,
        IRunInstrumentation instrumentation,
        CancellationToken cancellationToken)
    {
        var step1Result = await stepRunner.ExecuteAsync(
            step1,
            new Step1Input(options),
            context,
            instrumentation,
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
            cancellationToken);

        return CanContinue(step4Result) ? RunStatus.Success : RunStatus.Failed;
    }

    private static bool CanContinue<TOutput>(StepExecutionResult<TOutput> result)
    {
        return !result.HasErrors && result.HasUsableOutput;
    }

    private static readonly TimeZoneInfo Eastern = ResolveEastern();

    private static string TimeLabel => ReferenceEquals(Eastern, TimeZoneInfo.Utc) ? "UTC" : "ET";

    private static string FormatRunTimestamp(DateTimeOffset value)
    {
        var timestamp = TimeZoneInfo.ConvertTime(value, Eastern);
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo ResolveEastern()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("America/New_York", out var iana))
        {
            return iana;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById("Eastern Standard Time", out var windows)
            ? windows
            : TimeZoneInfo.Utc;
    }
}
