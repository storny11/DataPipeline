// Maps Step 2 output into the Step 3 source request, reporting request-mapping warnings at origin.
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using RunReporting;

namespace DataRetriever.Application.Step3Load;

public sealed class Step3RequestMapper(ExternalId2Normalizer normalizer, IRunReporter reporter)
{
    public Step3RequestMappingResult Map(Step2Output input)
    {
        var values = new List<string>();
        var invalidRows = 0;

        foreach (var row in input.Records)
        {
            if (normalizer.TryNormalize(row.ExternalId2, out var normalized))
            {
                values.Add(normalized.Value);
                continue;
            }

            invalidRows++;
            reporter.AddIssue(
                Step3Loader.StepName,
                IssueKey(row.ExternalId2),
                "Step 3 request could not be built because external id 2 is missing or invalid.",
                Subject(row));
        }

        return new Step3RequestMappingResult(
            new Step3RequestDto(values.Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
            invalidRows);
    }

    internal static object Subject(Step2OutputRecord row)
    {
        return new
        {
            internalId = row.InternalId,
            externalId1 = row.ExternalId1,
            externalId2 = row.ExternalId2
        };
    }

    internal static string IssueKey(string? externalId2)
    {
        return string.IsNullOrWhiteSpace(externalId2)
            ? "missing-external-id-2"
            : externalId2.Trim();
    }
}

public sealed record Step3RequestMappingResult(
    Step3RequestDto Request,
    int InvalidRowCount);
