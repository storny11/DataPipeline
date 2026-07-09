// Checks whether Step 3 returned data for requested identifiers, reporting gaps at origin.
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using RunReporting;

namespace DataRetriever.Application.Step3Load;

public sealed class Step3ResponseValidator(ExternalId2Normalizer normalizer, IRunReporter reporter)
{
    public void ValidateRequestedRowsReturned(
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

        foreach (var row in input.Records)
        {
            if (!normalizer.TryNormalize(row.ExternalId2, out var normalized) ||
                returned.Contains(normalized))
            {
                continue;
            }

            reporter.AddIssue(
                Step3Loader.StepName,
                normalized.Value,
                $"Step 3 source did not return data for requested external id 2 '{row.ExternalId2}'.",
                Step3RequestMapper.Subject(row));
        }
    }
}
