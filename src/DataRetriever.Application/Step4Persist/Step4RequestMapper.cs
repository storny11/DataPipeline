using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;

namespace DataRetriever.Application.Step4Persist;

public sealed class Step4RequestMapper
{
    public Step4RequestMappingResult Map(IReadOnlyList<Step3OutputRecord> records)
    {
        var request = new List<Step4RequestDto>();
        var sourceRecords = new List<Step3OutputRecord>();
        var issues = new List<StepIssue>();

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.InternalId) ||
                string.IsNullOrWhiteSpace(record.ExternalId1) ||
                string.IsNullOrWhiteSpace(record.ExternalId2))
            {
                issues.Add(new StepIssue(
                    Step4Persister.StepName,
                    StepIssueSeverity.Warning,
                    "Persistence request row is missing an identifier and was discarded.",
                    Context(record)));
                continue;
            }

            request.Add(new Step4RequestDto(
                record.InternalId,
                record.ExternalId1,
                record.ExternalId2,
                record.Amount1,
                record.Amount2,
                record.Amount3));
            sourceRecords.Add(record);
        }

        return new Step4RequestMappingResult(request, sourceRecords, issues);
    }

    public static DiagnosticContext Context(Step3OutputRecord record)
    {
        return DiagnosticContext.From(
            ("internalId", record.InternalId),
            ("externalId1", record.ExternalId1),
            ("externalId2", record.ExternalId2));
    }
}

public sealed record Step4RequestMappingResult(
    IReadOnlyList<Step4RequestDto> Request,
    IReadOnlyList<Step3OutputRecord> SourceRecords,
    IReadOnlyList<StepIssue> Issues);
