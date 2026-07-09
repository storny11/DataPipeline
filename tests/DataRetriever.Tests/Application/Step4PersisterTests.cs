// Verifies Step 4 all-or-nothing persistence accounting.
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using RunReporting;

namespace DataRetriever.Tests.Application;

public sealed class Step4PersisterTests
{
    [Fact]
    public async Task ExecuteAsync_WithDuplicateIdentifiers_CountsEachPersistedRow()
    {
        var reporter = new RunReporter(
            new RunReportingOptions(),
            [],
            NullLogger<RunReporter>.Instance);
        var persister = new Step4Persister(
            new FakeStep4SinkClient(),
            new Step4RequestMapper(reporter),
            reporter);

        var result = await persister.ExecuteAsync(
            new Step3Output([
                new Step3OutputRecord("INT-1", "EXT1-A", "EXT2-A", 1, 2, 3),
                new Step3OutputRecord("INT-1", "EXT1-A", "EXT2-A", 4, 5, 6)
            ]),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(2, result.Output!.PersistedRecords.Count);
        Assert.Equal(2, result.Counters.Single(counter => counter.Name == "RowsSuccessfullyPersisted").Value);

        var table = Assert.Single(reporter.Take().Tables);
        Assert.Equal("Persisted Records", table.Title);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("INT-1", table.Rows[0][0]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSinkThrows_ReturnsFailedResult()
    {
        var failedRunReporter = new RunReporter(
            new RunReportingOptions(),
            [],
            NullLogger<RunReporter>.Instance);
        var persister = new Step4Persister(
            new FakeStep4SinkClient(_ => throw new InvalidOperationException("Sink unavailable.")),
            new Step4RequestMapper(failedRunReporter),
            failedRunReporter);

        var result = await persister.ExecuteAsync(
            new Step3Output([
                new Step3OutputRecord("INT-1", "EXT1-A", "EXT2-A", 1, 2, 3),
                new Step3OutputRecord("INT-2", "EXT1-B", "EXT2-B", 4, 5, 6)
            ]),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Failed, result.Status);
        Assert.Null(result.Output);
        Assert.Contains(result.Issues, issue =>
            issue.Severity == StepIssueSeverity.Error &&
            issue.Message.Contains("Sink unavailable", StringComparison.Ordinal));
        Assert.Equal(0, result.Counters.Single(counter => counter.Name == "RowsSuccessfullyPersisted").Value);
        Assert.Empty(failedRunReporter.Take().Tables);
    }

    private sealed class FakeStep4SinkClient(
        Action<IReadOnlyList<Step4RequestDto>>? persist = null) : IStep4SinkClient
    {
        public Task PersistAsync(
            IReadOnlyList<Step4RequestDto> records,
            CancellationToken cancellationToken)
        {
            persist?.Invoke(records);
            return Task.CompletedTask;
        }
    }
}
