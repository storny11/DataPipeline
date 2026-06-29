// Verifies Step 1 filtering and warning behavior.
using DataRetriever.Application.Runs;
using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Tests.Application;

public sealed class Step1LoaderTests
{
    [Fact]
    public async Task ExecuteAsync_ValidatesFullDatasetBeforeFiltering()
    {
        var loader = new Step1Loader(
            new Source([
                new("INT-001", "EXT1-AAA", "GBP", "1"),
                new("INT-002", "EXT1-BBB", null, "1"),
                new("INT-003", "EXT1-CCC", "GBP", "not-a-number")
            ]),
            new Step1Validator(),
            new Step1Mapper());

        var result = await loader.ExecuteAsync(
            new Step1Input(DataRetrievalRunOptions.FromRequest("GBP", null)),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.SucceededWithIssues, result.Status);
        Assert.Single(result.Output!.Records);
        Assert.Equal("INT-001", result.Output.Records[0].InternalId);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("currency", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.Message.Contains("records-to-keep", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_WithNoValidConfiguredRows_ReturnsFailedResult()
    {
        var loader = new Step1Loader(
            new Source([
                new(null, "EXT1-AAA", "GBP", "1"),
                new("INT-002", "EXT1-BBB", null, "not-a-number")
            ]),
            new Step1Validator(),
            new Step1Mapper());

        var result = await loader.ExecuteAsync(
            new Step1Input(DataRetrievalRunOptions.FromRequest(null, null)),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Failed, result.Status);
        Assert.False(result.HasUsableOutput);
        Assert.Null(result.Output);
        Assert.Contains(result.Issues, issue => issue.Severity == StepIssueSeverity.Error);
        Assert.Contains(result.Counters, counter => counter.Name == "ValidConfiguredRows" && counter.Value == 0);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInput_Throws()
    {
        var loader = new Step1Loader(
            new Source([]),
            new Step1Validator(),
            new Step1Mapper());

        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.ExecuteAsync(
            null!,
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None));
    }

    private sealed class Source : IStep1SourceClient
    {
        private readonly IReadOnlyList<Step1Dto> _rows;

        public Source(IReadOnlyList<Step1Dto> rows)
        {
            _rows = rows;
        }

        public Task<IReadOnlyList<Step1Dto>> LoadConfiguredDataAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_rows);
        }
    }
}
