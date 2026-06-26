using DataRetriever.Execution;
using DataRetriever.Monitoring;
using Microsoft.Extensions.Logging;

namespace DataRetriever.Application.Runs;

public sealed class StepRunner(
    RunInstrumentationWriter instrumentationWriter,
    ILogger<StepRunner> logger)
{
    public async Task<StepExecutionResult<TOutput>> ExecuteAsync<TInput, TOutput>(
        IStep<TInput, TOutput> step,
        TInput input,
        RunContext context,
        IRunInstrumentation instrumentation,
        ICollection<IStepExecutionResult> results,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting {StepName} for run {RunId}", step.Name, context.RunId);

        var result = await step.ExecuteAsync(input, context, cancellationToken);
        results.Add(result);

        LogIssues(result);
        instrumentationWriter.RecordStepResult(instrumentation, result);

        logger.LogInformation("Completed {StepName} for run {RunId} with {Status}", step.Name, context.RunId, result.Status);
        return result;
    }

    private void LogIssues(IStepExecutionResult result)
    {
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
    }
}
