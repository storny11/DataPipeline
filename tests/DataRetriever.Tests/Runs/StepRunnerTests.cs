// Verifies StepRunner bridges step-result issues into the run reporter.
using DataRetriever.Application.Runs;
using DataRetriever.Execution;
using DataRetriever.Monitoring;
using Microsoft.Extensions.Logging.Abstractions;
using RunReporting;

namespace DataRetriever.Tests.Runs;

public sealed class StepRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsStepResultIssuesToReporter()
    {
        var reporter = new RunReporter(new RunReportingOptions(), []);
        var runner = new StepRunner(
            new RunInstrumentationWriter(),
            reporter,
            NullLogger<StepRunner>.Instance);
        var context = new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var instrumentation = new InMemoryProcessingTracker().ForRun(context.RunId);
        var results = new List<IStepExecutionResult>();

        var result = await runner.ExecuteAsync(
            new FailingStep(),
            NoInput.Value,
            context,
            instrumentation,
            results,
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Failed, result.Status);
        Assert.Same(result, Assert.Single(results));

        var bridged = Assert.Single(reporter.Take().Issues);
        Assert.Equal("FailingStep", bridged.StepName);
        Assert.Equal(IssueSeverity.Error, bridged.Severity);
        Assert.Equal("Source unavailable.", bridged.Message);
        Assert.Equal("7", bridged.Data["row"]);
    }

    private sealed class FailingStep : IStep<NoInput, string>
    {
        public string Name => "FailingStep";

        public Task<StepExecutionResult<string>> ExecuteAsync(
            NoInput input,
            RunContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(StepExecutionResult<string>.Failed(
                Name,
                [
                    new StepIssue(
                        Name,
                        StepIssueSeverity.Error,
                        "Source unavailable.",
                        DiagnosticContext.From(("row", "7")))
                ]));
        }
    }
}
