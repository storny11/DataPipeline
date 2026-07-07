// Maps final records, calls the sink, and treats a completed sink call as all attempted rows persisted.
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;
using RunReporting;

namespace DataRetriever.Application.Step4Persist;

public sealed class Step4Persister(
    IStep4SinkClient sinkClient,
    Step4RequestMapper mapper,
    IRunReporter reporter) : IStep<Step3Output, Step4Output>
{
    public const string StepName = "Step4Persist";

    public string Name => StepName;

    public async Task<StepExecutionResult<Step4Output>> ExecuteAsync(
        Step3Output input,
        RunContext context,
        CancellationToken cancellationToken)
    {
        var mapped = mapper.Map(input.Records);
        var rowsDiscarded = input.Records.Count - mapped.Request.Count;

        if (mapped.Request.Count == 0)
        {
            AddPersistedRecordsTable([]);
            return StepExecutionResult<Step4Output>.FromOutput(
                Name,
                new Step4Output([]),
                Counters(0, rowsDiscarded, 0));
        }

        try
        {
            await sinkClient.PersistAsync(mapped.Request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StepExecutionResult<Step4Output>.Failed(
                Name,
                [
                    new StepIssue(
                        Name,
                        StepIssueSeverity.Error,
                        $"Persistence request failed: {exception.Message}",
                        DiagnosticContext.From(("recordsAttempted", mapped.Request.Count.ToString())))
                ],
                Counters(mapped.Request.Count, rowsDiscarded, 0));
        }

        AddPersistedRecordsTable(mapped.SourceRecords);
        return StepExecutionResult<Step4Output>.FromOutput(
            Name,
            new Step4Output(mapped.SourceRecords),
            Counters(mapped.Request.Count, rowsDiscarded, mapped.SourceRecords.Count));
    }

    private void AddPersistedRecordsTable(IReadOnlyList<Step3OutputRecord> records)
    {
        reporter.AddTable(
            "Persisted Records",
            ["internalId", "externalId1", "externalId2", "amount1", "amount2", "amount3"],
            records);
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
