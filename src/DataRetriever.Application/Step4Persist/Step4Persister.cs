// Maps final records, calls the sink, and treats a completed sink call as all attempted rows persisted.
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step4Persist;

public sealed class Step4Persister(
    IStep4SinkClient sinkClient,
    Step4RequestMapper mapper) : IStep<Step3Output, Step4Output>
{
    public const string StepName = "Step4Persist";

    public string Name => StepName;

    public async Task<StepExecutionResult<Step4Output>> ExecuteAsync(
        Step3Output input,
        RunContext context,
        CancellationToken cancellationToken)
    {
        var mapped = mapper.Map(input.Records);
        var issues = new List<StepIssue>(mapped.Issues);

        if (mapped.Request.Count == 0)
        {
            return StepExecutionResult<Step4Output>.FromOutput(
                Name,
                new Step4Output([]),
                Counters(0, mapped.Issues.Count, 0),
                issues);
        }

        try
        {
            await sinkClient.PersistAsync(mapped.Request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            issues.Add(new StepIssue(
                Name,
                StepIssueSeverity.Error,
                $"Persistence request failed: {exception.Message}",
                DiagnosticContext.From(("recordsAttempted", mapped.Request.Count.ToString()))));

            return StepExecutionResult<Step4Output>.Failed(
                Name,
                issues,
                Counters(mapped.Request.Count, mapped.Issues.Count, 0));
        }

        return StepExecutionResult<Step4Output>.FromOutput(
            Name,
            new Step4Output(mapped.SourceRecords),
            Counters(mapped.Request.Count, mapped.Issues.Count, mapped.SourceRecords.Count),
            issues);
    }

    private static StepCounter[] Counters(
        int rowsAttempted,
        int rowsDiscardedDueToMappingErrors,
        int rowsPersisted) =>
        [
            new StepCounter("RowsAttemptedForPersistence", rowsAttempted),
            new StepCounter("RowsDiscardedDueToPersistenceMappingErrors", rowsDiscardedDueToMappingErrors),
            new StepCounter("RowsSuccessfullyPersisted", rowsPersisted)
        ];
}
