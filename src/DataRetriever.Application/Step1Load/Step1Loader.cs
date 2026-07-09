// Loads source-selected configured data, validates it, and maps it to Step 1 output.
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
        ArgumentNullException.ThrowIfNull(input);

        var rows = await sourceClient.LoadConfiguredDataAsync(input.RunOptions, cancellationToken);
        var validRows = validator.Validate(rows);
        var mappedRows = validRows.Select(mapper.Map).ToList();

        var counters = new[]
        {
            new StepCounter("ConfiguredRowsReturned", rows.Count),
            new StepCounter("InvalidConfiguredRows", rows.Count - validRows.Count),
            new StepCounter("ValidConfiguredRows", validRows.Count),
            new StepCounter("RowsAfterFiltering", rows.Count),
            new StepCounter("ValidRowsSelected", mappedRows.Count),
            new StepCounter("InvalidRowsDiscarded", rows.Count - validRows.Count)
        };

        var hasFilter = !string.IsNullOrWhiteSpace(input.RunOptions.Currency) ||
            input.RunOptions.InternalIds.Count > 0;
        if (validRows.Count == 0 && (!hasFilter || rows.Count > 0))
        {
            return StepExecutionResult<Step1Output>.Failed(
                Name,
                [
                    new StepIssue(
                        Name,
                        "Source",
                        "configured rows",
                        StepIssueSeverity.Error,
                        "No valid configured rows were available after validating the selected Step 1 source rows.")
                ],
                counters);
        }

        return StepExecutionResult<Step1Output>.FromOutput(
            Name,
            new Step1Output(mappedRows),
            counters);
    }

}
