// Executes one step, forwards step-result issues to the run reporter, and updates instrumentation.
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using Microsoft.Extensions.Logging;
using RunReporting;

namespace DataRetriever.Application.Runs;

public sealed class StepRunner(
    RunInstrumentationWriter instrumentationWriter,
    IRunReporter runReporter,
    ILogger<StepRunner> logger)
{
    public async Task<StepExecutionResult<TOutput>> ExecuteAsync<TInput, TOutput>(
        IStep<TInput, TOutput> step,
        TInput input,
        RunContext context,
        IRunInstrumentation instrumentation,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting {StepName} for run {RunId}", step.Name, context.RunId);

        var result = await step.ExecuteAsync(input, context, cancellationToken);

        // Steps report ordinary issues at origin; the fatal errors carried in step results
        // are bridged here so the published report contains everything.
        foreach (var issue in result.Issues)
        {
            runReporter.AddIssue(
                issue.Context.Values,
                issue.Message,
                issue.StepName,
                issue.Severity == StepIssueSeverity.Error ? IssueSeverity.Error : IssueSeverity.Warning);
        }

        instrumentationWriter.RecordStepResult(instrumentation, result);

        logger.LogInformation("Completed {StepName} for run {RunId} with {Status}", step.Name, context.RunId, result.Status);
        return result;
    }
}
