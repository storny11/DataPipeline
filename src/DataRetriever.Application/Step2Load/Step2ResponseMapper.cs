// Maps Step 2 response DTOs into internal output records, reporting discards at origin.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;
using RunReporting;

namespace DataRetriever.Application.Step2Load;

public sealed class Step2ResponseMapper(IRunReporter reporter)
{
    public IReadOnlyList<Step2OutputRecord> Map(
        Step1OutputRecord input,
        IReadOnlyList<Step2ResponseDto> rows)
    {
        var records = new List<Step2OutputRecord>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ExternalId2))
            {
                reporter.AddIssue(
                    Subject(input),
                    "Step 2 source row is missing external id 2 and was discarded.",
                    Step2Loader.StepName);
                continue;
            }

            records.Add(new Step2OutputRecord(
                input.InternalId,
                input.ExternalId1,
                row.ExternalId2.Trim(),
                row.EffectiveDate));
        }

        return records;
    }

    internal static object Subject(Step1OutputRecord input)
    {
        return new
        {
            internalId = input.InternalId,
            externalId1 = input.ExternalId1
        };
    }
}
