// Fetches, maps, selects, and warns for Step 2 related data.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step2Load;

public sealed class Step2Loader(
    IStep2SourceClient sourceClient,
    Step2ResponseMapper mapper,
    Step2Selector selector) : IStep<Step1Output, Step2Output>
{
    public const string StepName = "Step2Load";

    public string Name => StepName;

    public async Task<StepExecutionResult<Step2Output>> ExecuteAsync(
        Step1Output input,
        RunContext context,
        CancellationToken cancellationToken)
    {
        var outputRecords = new List<Step2OutputRecord>();
        var issues = new List<StepIssue>();
        var sourceCalls = 0;
        var rowsDiscarded = 0;

        foreach (var row in input.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sourceCalls++;

            IReadOnlyList<Step2ResponseDto> sourceRows;
            try
            {
                sourceRows = await sourceClient.FetchRelatedDataAsync(row, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                issues.Add(new StepIssue(
                    Name,
                    StepIssueSeverity.Warning,
                    $"Step 2 source call failed for external id 1 '{row.ExternalId1}': {exception.Message}",
                    Step2ResponseMapper.Context(row)));
                continue;
            }

            if (sourceRows.Count == 0)
            {
                issues.Add(new StepIssue(
                    Name,
                    StepIssueSeverity.Warning,
                    $"Step 2 source returned no rows for external id 1 '{row.ExternalId1}'.",
                    Step2ResponseMapper.Context(row)));
                continue;
            }

            var mapped = mapper.Map(row, sourceRows);
            issues.AddRange(mapped.Issues);
            rowsDiscarded += sourceRows.Count - mapped.Records.Count;

            var selected = selector.SelectLatest(mapped.Records, row.Step2RecordsToKeep);
            if (selected.Count < row.Step2RecordsToKeep)
            {
                issues.Add(new StepIssue(
                    Name,
                    StepIssueSeverity.Warning,
                    $"Step 2 source returned {selected.Count} valid rows for external id 1 '{row.ExternalId1}', fewer than requested {row.Step2RecordsToKeep}.",
                    Step2ResponseMapper.Context(row)));
            }

            outputRecords.AddRange(selected);
        }

        var counters = new[]
        {
            new StepCounter("Step1RowsProcessed", input.Records.Count),
            new StepCounter("Step2SourceCallsAttempted", sourceCalls),
            new StepCounter("Step2RowsProduced", outputRecords.Count),
            new StepCounter("Step2RowsDiscarded", rowsDiscarded)
        };

        return StepExecutionResult<Step2Output>.Success(
            Name,
            new Step2Output(outputRecords),
            counters,
            issues);
    }
}
