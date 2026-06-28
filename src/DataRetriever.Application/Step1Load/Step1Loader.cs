// Loads configured data, maps it to Step 1 output, and applies request filters.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step1Load;

public sealed class Step1Loader(
    IStep1SourceClient sourceClient,
    Step1Validator validator,
    Step1Mapper mapper) : IStep<Step1Input, Step1Output>
{
    public const string StepName = "Step1Load";

    public string Name => StepName;

    public async Task<StepExecutionResult<Step1Output>> ExecuteAsync(
        Step1Input input,
        RunContext context,
        CancellationToken cancellationToken)
    {
        var rows = await sourceClient.LoadConfiguredDataAsync(cancellationToken);
        var validation = validator.Validate(rows);
        var mappedRows = validation.ValidRows.Select(mapper.Map).ToList();
        var filteredRows = ApplyFilter(mappedRows, input).ToList();

        var counters = new[]
        {
            new StepCounter("ConfiguredRowsReturned", rows.Count),
            new StepCounter("InvalidConfiguredRows", rows.Count - validation.ValidRows.Count),
            new StepCounter("ValidConfiguredRows", validation.ValidRows.Count),
            new StepCounter("RowsAfterFiltering", filteredRows.Count),
            new StepCounter("ValidRowsSelected", filteredRows.Count),
            new StepCounter("InvalidRowsDiscarded", rows.Count - validation.ValidRows.Count)
        };

        return StepExecutionResult<Step1Output>.FromOutput(
            Name,
            new Step1Output(filteredRows),
            counters,
            validation.Issues);
    }

    private static IEnumerable<Step1OutputRecord> ApplyFilter(
        IEnumerable<Step1OutputRecord> rows,
        Step1Input input)
    {
        if (!string.IsNullOrWhiteSpace(input.RunOptions.Currency))
        {
            return rows.Where(row => string.Equals(
                row.Currency,
                input.RunOptions.Currency,
                StringComparison.OrdinalIgnoreCase));
        }

        if (input.RunOptions.InternalIds.Count > 0)
        {
            var ids = input.RunOptions.InternalIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return rows.Where(row => ids.Contains(row.InternalId));
        }

        return rows;
    }
}
