// Verifies Step 4 row-level persistence accounting and sink outcome validation.
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;

namespace DataRetriever.Tests.Application;

public sealed class Step4PersisterTests
{
    [Fact]
    public async Task ExecuteAsync_WithDuplicateIdentifiers_CountsEachPersistedRow()
    {
        var persister = new Step4Persister(
            new FakeStep4SinkClient(records => Step4PersistResult.AllSucceeded(records.Count)),
            new Step4RequestMapper());

        var result = await persister.ExecuteAsync(
            new Step3Output([
                new Step3OutputRecord("INT-1", "EXT1-A", "EXT2-A", 1, 2, 3),
                new Step3OutputRecord("INT-1", "EXT1-A", "EXT2-A", 4, 5, 6)
            ]),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(2, result.Output!.PersistedRecords.Count);
        Assert.Equal(2, result.Counters.Single(counter => counter.Name == "RowsSuccessfullyPersisted").Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSinkOmitsOutcomeForAttemptedRow_ReturnsError()
    {
        var persister = new Step4Persister(
            new FakeStep4SinkClient(_ => new Step4PersistResult([
                new Step4PersistRecordResult(0, true)
            ])),
            new Step4RequestMapper());

        var result = await persister.ExecuteAsync(
            new Step3Output([
                new Step3OutputRecord("INT-1", "EXT1-A", "EXT2-A", 1, 2, 3),
                new Step3OutputRecord("INT-2", "EXT1-B", "EXT2-B", 4, 5, 6)
            ]),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Failed, result.Status);
        Assert.Single(result.Output!.PersistedRecords);
        Assert.Contains(result.Issues, issue =>
            issue.Severity == StepIssueSeverity.Error &&
            issue.Message.Contains("did not return an outcome", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSinkReturnsDuplicateOutcomesForAttemptedRow_ReturnsError()
    {
        var persister = new Step4Persister(
            new FakeStep4SinkClient(_ => new Step4PersistResult([
                new Step4PersistRecordResult(0, true),
                new Step4PersistRecordResult(0, true),
                new Step4PersistRecordResult(1, true)
            ])),
            new Step4RequestMapper());

        var result = await persister.ExecuteAsync(
            new Step3Output([
                new Step3OutputRecord("INT-1", "EXT1-A", "EXT2-A", 1, 2, 3),
                new Step3OutputRecord("INT-2", "EXT1-B", "EXT2-B", 4, 5, 6)
            ]),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Failed, result.Status);
        Assert.Single(result.Output!.PersistedRecords);
        Assert.Contains(result.Issues, issue =>
            issue.Severity == StepIssueSeverity.Error &&
            issue.Message.Contains("Exactly one outcome is required", StringComparison.Ordinal));
    }

    private sealed class FakeStep4SinkClient(
        Func<IReadOnlyList<Step4RequestDto>, Step4PersistResult> resultFactory) : IStep4SinkClient
    {
        public Task<Step4PersistResult> PersistAsync(
            IReadOnlyList<Step4RequestDto> records,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(resultFactory(records));
        }
    }
}
