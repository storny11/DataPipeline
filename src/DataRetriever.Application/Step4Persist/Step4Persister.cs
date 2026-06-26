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
            return StepExecutionResult<Step4Output>.Success(
                Name,
                new Step4Output([]),
                [
                    new StepCounter("RowsAttemptedForPersistence", 0),
                    new StepCounter("RowsDiscardedDueToPersistenceMappingErrors", mapped.Issues.Count),
                    new StepCounter("RowsSuccessfullyPersisted", 0)
                ],
                issues);
        }

        IReadOnlyList<Step4RequestDto> persisted;
        try
        {
            persisted = await sinkClient.PersistAsync(mapped.Request, cancellationToken);
        }
        catch (Exception exception)
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

        var persistedKeys = persisted
            .Select(record => $"{record.InternalId}|{record.ExternalId1}|{record.ExternalId2}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var persistedOutput = input.Records
            .Where(record => persistedKeys.Contains($"{record.InternalId}|{record.ExternalId1}|{record.ExternalId2}"))
            .ToList();

        return StepExecutionResult<Step4Output>.Success(
            Name,
            new Step4Output(persistedOutput),
            [
                new StepCounter("RowsAttemptedForPersistence", mapped.Request.Count),
                new StepCounter("RowsDiscardedDueToPersistenceMappingErrors", mapped.Issues.Count),
                new StepCounter("RowsSuccessfullyPersisted", persistedOutput.Count)
            ],
            issues);
    }
}
