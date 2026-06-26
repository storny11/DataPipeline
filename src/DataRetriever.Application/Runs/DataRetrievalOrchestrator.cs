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
    RunReportBuilder reportBuilder,
    IRunReportPublisher reportPublisher,
    ILogger<DataRetrievalOrchestrator> logger)
{
    public async Task<RunReport> RunAsync(
        DataRetrievalRunOptions options,
        CancellationToken cancellationToken)
    {
        var context = new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var instrumentation = processingTracker.ForRun(context.RunId);
        AppendStatus(instrumentation, "Running");

        var results = new List<IStepExecutionResult>();
        IReadOnlyList<PersistedRecordSummary> persistedRecords = [];
        var finalStatus = "Success";

        try
        {
            var step1Result = await ExecuteStepAsync(
                step1,
                new Step1Input(options),
                context,
                instrumentation,
                results,
                cancellationToken);

            if (!CanContinue(step1Result))
            {
                finalStatus = "Failed";
                return await FinishAsync(context, options, results, persistedRecords, finalStatus, instrumentation, cancellationToken);
            }

            var step2Result = await ExecuteStepAsync(
                step2,
                step1Result.Output!,
                context,
                instrumentation,
                results,
                cancellationToken);

            if (!CanContinue(step2Result))
            {
                finalStatus = "Failed";
                return await FinishAsync(context, options, results, persistedRecords, finalStatus, instrumentation, cancellationToken);
            }

            var step3Result = await ExecuteStepAsync(
                step3,
                step2Result.Output!,
                context,
                instrumentation,
                results,
                cancellationToken);

            if (!CanContinue(step3Result))
            {
                finalStatus = "Failed";
                return await FinishAsync(context, options, results, persistedRecords, finalStatus, instrumentation, cancellationToken);
            }

            var step4Result = await ExecuteStepAsync(
                step4,
                step3Result.Output!,
                context,
                instrumentation,
                results,
                cancellationToken);

            if (!CanContinue(step4Result))
            {
                finalStatus = "Failed";
            }

            persistedRecords = step4Result.Output?.PersistedRecords
                .Select(record => new PersistedRecordSummary(
                    record.InternalId,
                    record.ExternalId1,
                    record.ExternalId2,
                    record.Amount1,
                    record.Amount2,
                    record.Amount3))
                .ToList() ?? [];

            return await FinishAsync(context, options, results, persistedRecords, finalStatus, instrumentation, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unexpected data retrieval run failure for {RunId}", context.RunId);
            finalStatus = "Failed";
            results.Add(StepExecutionResult<NoOutput>.Failed(
                "Run",
                [
                    new StepIssue(
                        "Run",
                        StepIssueSeverity.Error,
                        $"Unexpected run failure: {exception.Message}",
                        DiagnosticContext.From(("runId", context.RunId.ToString())))
                ]));

            return await FinishAsync(context, options, results, persistedRecords, finalStatus, instrumentation, cancellationToken);
        }
    }

    private async Task<StepExecutionResult<TOutput>> ExecuteStepAsync<TInput, TOutput>(
        IStep<TInput, TOutput> step,
        TInput input,
        RunContext context,
        IRunInstrumentation instrumentation,
        List<IStepExecutionResult> results,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting {StepName} for run {RunId}", step.Name, context.RunId);
        var result = await step.ExecuteAsync(input, context, cancellationToken);
        results.Add(result);

        foreach (var issue in result.Issues)
        {
            var logLevel = issue.Severity == StepIssueSeverity.Error
                ? LogLevel.Error
                : LogLevel.Warning;
            logger.Log(
                logLevel,
                "{StepName} {Severity}: {Message}. Context: {@Context}",
                issue.StepName,
                issue.Severity,
                issue.Message,
                issue.Context.Values);
        }

        var info = new BasicInstrumentationInfo();
        info.AddValue("Status", result.Status.ToString());
        info.AddValue("Warnings", result.Issues.Count(issue => issue.Severity == StepIssueSeverity.Warning));
        info.AddValue("Errors", result.Issues.Count(issue => issue.Severity == StepIssueSeverity.Error));
        foreach (var counter in result.Counters)
        {
            info.AddValue(counter.Name, counter.Value);
        }

        instrumentation.AppendInstrumentationInfo(result.StepName, info);
        logger.LogInformation("Completed {StepName} for run {RunId} with {Status}", step.Name, context.RunId, result.Status);
        return result;
    }

    private async Task<RunReport> FinishAsync(
        RunContext context,
        DataRetrievalRunOptions options,
        IReadOnlyList<IStepExecutionResult> results,
        IReadOnlyList<PersistedRecordSummary> persistedRecords,
        string status,
        IRunInstrumentation instrumentation,
        CancellationToken cancellationToken)
    {
        AppendStatus(instrumentation, status);

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

    private static bool CanContinue<TOutput>(StepExecutionResult<TOutput> result)
    {
        return !result.HasErrors && result.HasUsableOutput;
    }

    private static void AppendStatus(IRunInstrumentation instrumentation, string status)
    {
        var info = new BasicInstrumentationInfo();
        info.AddValue("Status", status);
        instrumentation.AppendInstrumentationInfo("run", info);
    }

    private sealed class BasicInstrumentationInfo : IInstrumentationInfo
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, object?> Values => _values;

        public void AddValue<T>(string name, T value)
        {
            _values[name] = value;
        }
    }
}
