// Checks whether Step 3 returned data for requested identifiers.
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step3Load;

public sealed class Step3ResponseValidator(ExternalId2Normalizer normalizer)
{
    public IReadOnlyList<StepIssue> ValidateRequestedRowsReturned(
        Step2Output input,
        Step3ResponseDto response)
    {
        var returned = response.Items
            .Select(item => normalizer.TryNormalize(item.ExternalId2, out var normalized)
                ? normalized
                : (NormalizedExternalId2?)null)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToHashSet();

        var issues = new List<StepIssue>();
        foreach (var row in input.Records)
        {
            if (!normalizer.TryNormalize(row.ExternalId2, out var normalized) ||
                returned.Contains(normalized))
            {
                continue;
            }

            issues.Add(new StepIssue(
                Step3Loader.StepName,
                StepIssueSeverity.Warning,
                $"Step 3 source did not return data for requested external id 2 '{row.ExternalId2}'.",
                Step3RequestMapper.Context(row)));
        }

        return issues;
    }
}
