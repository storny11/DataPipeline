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
            new FakeStep4SinkClient(),
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

    private sealed class FakeStep4SinkClient : IStep4SinkClient
    {
        public Task<Step4PersistResult> PersistAsync(
            IReadOnlyList<Step4RequestDto> records,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Step4PersistResult.AllSucceeded(records.Count));
        }
    }
}
