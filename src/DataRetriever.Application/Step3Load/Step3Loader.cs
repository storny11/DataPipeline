// Builds the Step 3 request, fetches amount data, maps responses, and joins rows back to Step 2 output.
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step3Load;

public sealed class Step3Loader(
    IStep3SourceClient sourceClient,
    Step3RequestMapper requestMapper,
    Step3ResponseValidator responseValidator,
    Step3ResponseMapper responseMapper,
    ExternalId2Normalizer normalizer) : IStep<Step2Output, Step3Output>
{
    public const string StepName = "Step3Load";

    public string Name => StepName;

    public async Task<StepExecutionResult<Step3Output>> ExecuteAsync(
        Step2Output input,
        RunContext context,
        CancellationToken cancellationToken)
    {
        var requestMapping = requestMapper.Map(input);

        Step3ResponseDto response;
        try
        {
            response = await sourceClient.FetchAmountsAsync(requestMapping.Request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StepExecutionResult<Step3Output>.Failed(
                Name,
                [
                    new StepIssue(
                        Name,
                        "source-request",
                        StepIssueSeverity.Error,
                        $"Step 3 source request failed: {exception.Message}",
                        DiagnosticContext.From(("requestedExternalId2Count", requestMapping.Request.ExternalId2Values.Count.ToString())))
                ],
                [
                    new StepCounter("ExternalId2ValuesRequested", requestMapping.Request.ExternalId2Values.Count)
                ]);
        }

        responseValidator.ValidateRequestedRowsReturned(input, response);
        var subjectByExternalId2 = BuildSubjectByExternalId2(input);
        var amounts = responseMapper.Map(response.Items, subjectByExternalId2);

        var output = new List<Step3OutputRecord>();
        foreach (var row in input.Records)
        {
            if (!normalizer.TryNormalize(row.ExternalId2, out var normalized) ||
                !amounts.TryGetValue(normalized, out var amount))
            {
                continue;
            }

            output.Add(new Step3OutputRecord(
                row.InternalId,
                row.ExternalId1,
                row.ExternalId2,
                amount.Amount1,
                amount.Amount2,
                amount.Amount3));
        }

        var missingStep3Rows = Math.Max(0, input.Records.Count - requestMapping.InvalidRowCount - output.Count);
        var counters = new[]
        {
            new StepCounter("ExternalId2ValuesRequested", requestMapping.Request.ExternalId2Values.Count),
            new StepCounter("Step3RowsReturned", response.Items.Count),
            new StepCounter("ValidStep3RowsReturned", output.Count),
            new StepCounter("RowsDiscardedDueToMissingAmounts", response.Items.Count - amounts.Count),
            new StepCounter("RowsDiscardedDueToMappingErrors", requestMapping.InvalidRowCount),
            new StepCounter("RowsMatchedToStep2Output", output.Count),
            new StepCounter("MissingStep3Rows", missingStep3Rows)
        };

        return StepExecutionResult<Step3Output>.FromOutput(
            Name,
            new Step3Output(output),
            counters);
    }

    private IReadOnlyDictionary<NormalizedExternalId2, object> BuildSubjectByExternalId2(Step2Output input)
    {
        var subjects = new Dictionary<NormalizedExternalId2, object>();
        foreach (var row in input.Records)
        {
            if (normalizer.TryNormalize(row.ExternalId2, out var normalized))
            {
                subjects[normalized] = Step3RequestMapper.Subject(row);
            }
        }

        return subjects;
    }
}
