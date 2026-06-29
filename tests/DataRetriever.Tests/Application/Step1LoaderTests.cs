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
            new Source(),
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

    private sealed class Source : IStep1SourceClient
    {
        public Task<IReadOnlyList<Step1Dto>> LoadConfiguredDataAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Step1Dto> rows =
            [
                new("INT-001", "EXT1-AAA", "GBP", "1"),
                new("INT-002", "EXT1-BBB", null, "1"),
                new("INT-003", "EXT1-CCC", "GBP", "not-a-number")
            ];

            return Task.FromResult(rows);
        }
    }
}
