// Maps Step 2 response DTOs into internal output records.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step2Load;

public sealed class Step2ResponseMapper
{
    public Step2MappingResult Map(
        Step1OutputRecord input,
        IReadOnlyList<Step2ResponseDto> rows)
    {
        var records = new List<Step2OutputRecord>();
        var issues = new List<StepIssue>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ExternalId2))
            {
                issues.Add(new StepIssue(
                    Step2Loader.StepName,
                    StepIssueSeverity.Warning,
                    "Step 2 source row is missing external id 2 and was discarded.",
                    Context(input)));
                continue;
            }

            records.Add(new Step2OutputRecord(
                input.InternalId,
                input.ExternalId1,
                row.ExternalId2.Trim(),
                row.EffectiveDate));
        }

        return new Step2MappingResult(records, issues);
    }

    public static DiagnosticContext Context(Step1OutputRecord input)
    {
        return DiagnosticContext.From(
            ("internalId", input.InternalId),
            ("externalId1", input.ExternalId1));
    }
}

public sealed record Step2MappingResult(
    IReadOnlyList<Step2OutputRecord> Records,
    IReadOnlyList<StepIssue> Issues);
