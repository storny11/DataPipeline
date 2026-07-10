// Verifies Step 1 filtering and warning behavior.
using DataRetriever.Application.Runs;
using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using RunReporting;

namespace DataRetriever.Tests.Application;

public sealed class Step1LoaderTests
{
    [Fact]
    public async Task ExecuteAsync_FiltersSourceRowsBeforeValidation()
    {
        var reporter = new RunReporter(new RunReportingOptions(), [], NullLogger<RunReporter>.Instance);
        var loader = new Step1Loader(
            new Source([
                new("INT-001", "EXT1-AAA", "GBP", "1"),
                new("INT-002", "EXT1-BBB", null, "1"),
                new("INT-003", "EXT1-CCC", "EUR", "not-a-number")
            ]),
            new Step1Validator(reporter),
            new Step1Mapper());

        var result = await loader.ExecuteAsync(
            new Step1Input(DataRetrievalRunOptions.FromRequest("GBP", null)),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Succeeded, result.Status);
        Assert.Single(result.Output!.Records);
        Assert.Equal("INT-001", result.Output.Records[0].InternalId);

        Assert.Empty((await reporter.CompleteAsync()).Issues);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsInvalidSelectedRows()
    {
        var reporter = new RunReporter(new RunReportingOptions(), [], NullLogger<RunReporter>.Instance);
        var loader = new Step1Loader(
            new Source([
                new("INT-001", "EXT1-AAA", "GBP", "1"),
                new("INT-002", "EXT1-BBB", "GBP", "not-a-number"),
                new("INT-003", "EXT1-CCC", "EUR", "not-a-number")
            ]),
            new Step1Validator(reporter),
            new Step1Mapper());

        var result = await loader.ExecuteAsync(
            new Step1Input(DataRetrievalRunOptions.FromRequest("GBP", null)),
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Succeeded, result.Status);
        Assert.Single(result.Output!.Records);

        var issue = Assert.Single((await reporter.CompleteAsync()).Issues);
        Assert.Equal("InternalId", issue.IdentifierName);
        Assert.Equal("INT-002", issue.IdentifierValue);
        Assert.Contains("records-to-keep", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoValidConfiguredRows_ReturnsFailedResult()
    {
        var reporter = new RunReporter(new RunReportingOptions(), [], NullLogger<RunReporter>.Instance);
        var loader = new Step1Loader(
            new Source([
                new(null, "EXT1-AAA", "GBP", "1"),
                new("INT-002", "EXT1-BBB", null, "not-a-number")
            ]),
            new Step1Validator(reporter),
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
        Assert.NotEmpty((await reporter.CompleteAsync()).Issues);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInput_Throws()
    {
        var reporter = new RunReporter(new RunReportingOptions(), [], NullLogger<RunReporter>.Instance);
        var loader = new Step1Loader(
            new Source([]),
            new Step1Validator(reporter),
            new Step1Mapper());

        await Assert.ThrowsAsync<ArgumentNullException>(() => loader.ExecuteAsync(
            null!,
            new RunContext(Guid.NewGuid(), DateTimeOffset.UtcNow),
            CancellationToken.None));
    }

    private sealed class Source : IStep1SourceClient
    {
        private readonly IReadOnlyList<Step1SourceRow> _rows;

        public Source(IReadOnlyList<Step1SourceRow> rows)
        {
            _rows = rows;
        }

        public Task<IReadOnlyList<Step1SourceRow>> LoadConfiguredDataAsync(
            DataRetrievalRunOptions selection,
            CancellationToken cancellationToken)
        {
            IEnumerable<Step1SourceRow> rows = _rows;
            if (!string.IsNullOrWhiteSpace(selection.Currency))
            {
                rows = rows.Where(row => string.Equals(
                    row.Currency?.Trim(),
                    selection.Currency,
                    StringComparison.OrdinalIgnoreCase));
            }
            else if (selection.InternalIds.Count > 0)
            {
                var ids = selection.InternalIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                rows = rows.Where(row => row.InternalId is not null && ids.Contains(row.InternalId.Trim()));
            }

            return Task.FromResult<IReadOnlyList<Step1SourceRow>>(rows.ToList());
        }
    }
}
