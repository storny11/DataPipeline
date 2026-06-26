using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step3Load;

public sealed class Step3RequestMapper(ExternalId2Normalizer normalizer)
{
    public Step3RequestMappingResult Map(Step2Output input)
    {
        var values = new List<string>();
        var issues = new List<StepIssue>();

        foreach (var row in input.Records)
        {
            if (normalizer.TryNormalize(row.ExternalId2, out var normalized))
            {
                values.Add(normalized.Value);
                continue;
            }

            issues.Add(new StepIssue(
                Step3Loader.StepName,
                StepIssueSeverity.Warning,
                "Step 3 request could not be built because external id 2 is missing or invalid.",
                Context(row)));
        }

        return new Step3RequestMappingResult(
            new Step3RequestDto(values.Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
            issues);
    }

    public static DiagnosticContext Context(Step2OutputRecord row)
    {
        return DiagnosticContext.From(
            ("internalId", row.InternalId),
            ("externalId1", row.ExternalId1),
            ("externalId2", row.ExternalId2));
    }
}

public sealed record Step3RequestMappingResult(
    Step3RequestDto Request,
    IReadOnlyList<StepIssue> Issues);
