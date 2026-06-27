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
        var issues = new List<StepIssue>(requestMapping.Issues);

        Step3ResponseDto response;
        try
        {
            response = await sourceClient.FetchAmountsAsync(requestMapping.Request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            issues.Add(new StepIssue(
                Name,
                StepIssueSeverity.Error,
                $"Step 3 source request failed: {exception.Message}",
                DiagnosticContext.From(("requestedExternalId2Count", requestMapping.Request.ExternalId2Values.Count.ToString()))));

            return StepExecutionResult<Step3Output>.Failed(Name, issues, [
                new StepCounter("ExternalId2ValuesRequested", requestMapping.Request.ExternalId2Values.Count)
            ]);
        }

        issues.AddRange(responseValidator.ValidateRequestedRowsReturned(input, response));
        var contextByExternalId2 = BuildContextByExternalId2(input);
        var mapped = responseMapper.Map(response.Items, contextByExternalId2);
        issues.AddRange(mapped.Issues);

        var output = new List<Step3OutputRecord>();
        foreach (var row in input.Records)
        {
            if (!normalizer.TryNormalize(row.ExternalId2, out var normalized) ||
                !mapped.Amounts.TryGetValue(normalized, out var amount))
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

        var missingStep3Rows = Math.Max(0, input.Records.Count - requestMapping.Issues.Count - output.Count);
        var counters = new[]
        {
            new StepCounter("ExternalId2ValuesRequested", requestMapping.Request.ExternalId2Values.Count),
            new StepCounter("Step3RowsReturned", response.Items.Count),
            new StepCounter("ValidStep3RowsReturned", output.Count),
            new StepCounter("RowsDiscardedDueToMissingAmounts", mapped.Issues.Count),
            new StepCounter("RowsDiscardedDueToMappingErrors", requestMapping.Issues.Count),
            new StepCounter("RowsMatchedToStep2Output", output.Count),
            new StepCounter("MissingStep3Rows", missingStep3Rows)
        };

        return StepExecutionResult<Step3Output>.Success(
            Name,
            new Step3Output(output),
            counters,
            issues);
    }

    private IReadOnlyDictionary<NormalizedExternalId2, DiagnosticContext> BuildContextByExternalId2(Step2Output input)
    {
        var contexts = new Dictionary<NormalizedExternalId2, DiagnosticContext>();
        foreach (var row in input.Records)
        {
            if (normalizer.TryNormalize(row.ExternalId2, out var normalized))
            {
                contexts[normalized] = Step3RequestMapper.Context(row);
            }
        }

        return contexts;
    }
}
