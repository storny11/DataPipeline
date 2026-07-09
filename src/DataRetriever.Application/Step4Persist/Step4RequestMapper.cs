// Maps Step 3 records into persistence requests, reporting discards at origin.
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist.Models;
using RunReporting;

namespace DataRetriever.Application.Step4Persist;

public sealed class Step4RequestMapper(IRunReporter reporter)
{
    public Step4RequestMappingResult Map(IReadOnlyList<Step3OutputRecord> records)
    {
        var request = new List<Step4RequestDto>();
        var sourceRecords = new List<Step3OutputRecord>();

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.InternalId) ||
                string.IsNullOrWhiteSpace(record.ExternalId1) ||
                string.IsNullOrWhiteSpace(record.ExternalId2))
            {
                var identifier = Identifier(record);
                reporter.AddIssue(
                    Step4Persister.StepName,
                    identifier.Name,
                    identifier.Value,
                    "Persistence request row is missing an identifier and was discarded.");
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

        return new Step4RequestMappingResult(request, sourceRecords);
    }

    private static (string Name, string Value) Identifier(Step3OutputRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.InternalId))
        {
            return ("InternalId", record.InternalId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(record.ExternalId1))
        {
            return ("ExternalId1", record.ExternalId1.Trim());
        }

        return !string.IsNullOrWhiteSpace(record.ExternalId2)
            ? ("ExternalId2", record.ExternalId2.Trim())
            : ("SourceRow", "unidentified");
    }
}

public sealed record Step4RequestMappingResult(
    IReadOnlyList<Step4RequestDto> Request,
    IReadOnlyList<Step3OutputRecord> SourceRecords);
