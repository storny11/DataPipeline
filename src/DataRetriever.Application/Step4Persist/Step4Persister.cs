// Maps final records, calls the sink, and converts persistence outcomes into step output and issues.
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
                [
                    new StepCounter("RowsAttemptedForPersistence", 0),
                    new StepCounter("RowsDiscardedDueToPersistenceMappingErrors", mapped.Issues.Count),
                    new StepCounter("RowsSuccessfullyPersisted", 0)
                ],
                issues);
        }

        Step4PersistResult persistResult;
        try
        {
            persistResult = await sinkClient.PersistAsync(mapped.Request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            issues.Add(new StepIssue(
                Name,
                StepIssueSeverity.Error,
                $"Persistence request failed: {exception.Message}",
                DiagnosticContext.From(("recordsAttempted", mapped.Request.Count.ToString()))));

            return StepExecutionResult<Step4Output>.Failed(Name, issues, [
                new StepCounter("RowsAttemptedForPersistence", mapped.Request.Count),
                new StepCounter("RowsDiscardedDueToPersistenceMappingErrors", mapped.Issues.Count),
                new StepCounter("RowsSuccessfullyPersisted", 0)
            ]);
        }

        var outcomesByRequestIndex = new Dictionary<int, List<Step4PersistRecordResult>>();
        foreach (var outcome in persistResult.Records)
        {
            if (outcome.RequestIndex < 0 || outcome.RequestIndex >= mapped.SourceRecords.Count)
            {
                issues.Add(new StepIssue(
                    Name,
                    StepIssueSeverity.Error,
                    $"Persistence sink returned an invalid request index '{outcome.RequestIndex}'.",
                    DiagnosticContext.From(("requestIndex", outcome.RequestIndex.ToString()))));
                continue;
            }

            if (!outcomesByRequestIndex.TryGetValue(outcome.RequestIndex, out var outcomes))
            {
                outcomes = [];
                outcomesByRequestIndex[outcome.RequestIndex] = outcomes;
            }

            outcomes.Add(outcome);
        }

        var persistedOutput = new List<Step3OutputRecord>();
        for (var requestIndex = 0; requestIndex < mapped.SourceRecords.Count; requestIndex++)
        {
            var sourceRecord = mapped.SourceRecords[requestIndex];
            if (!outcomesByRequestIndex.TryGetValue(requestIndex, out var outcomes))
            {
                issues.Add(new StepIssue(
                    Name,
                    StepIssueSeverity.Error,
                    $"Persistence sink did not return an outcome for request index '{requestIndex}'.",
                    Context(sourceRecord, requestIndex)));
                continue;
            }

            if (outcomes.Count > 1)
            {
                issues.Add(new StepIssue(
                    Name,
                    StepIssueSeverity.Error,
                    $"Persistence sink returned {outcomes.Count} outcomes for request index '{requestIndex}'. Exactly one outcome is required.",
                    Context(sourceRecord, requestIndex)));
                continue;
            }

            var outcome = outcomes[0];
            if (outcome.Succeeded)
            {
                persistedOutput.Add(sourceRecord);
                continue;
            }

            issues.Add(new StepIssue(
                Name,
                StepIssueSeverity.Error,
                string.IsNullOrWhiteSpace(outcome.Message)
                    ? "Persistence sink reported a row-level failure."
                    : outcome.Message,
                Context(sourceRecord, requestIndex)));
        }

        return StepExecutionResult<Step4Output>.FromOutput(
            Name,
            new Step4Output(persistedOutput),
            [
                new StepCounter("RowsAttemptedForPersistence", mapped.Request.Count),
                new StepCounter("RowsDiscardedDueToPersistenceMappingErrors", mapped.Issues.Count),
                new StepCounter("RowsSuccessfullyPersisted", persistedOutput.Count)
            ],
            issues);
    }

    private static DiagnosticContext Context(Step3OutputRecord record, int requestIndex)
    {
        return DiagnosticContext.From(
            ("requestIndex", requestIndex.ToString()),
            ("internalId", record.InternalId),
            ("externalId1", record.ExternalId1),
            ("externalId2", record.ExternalId2));
    }
}
