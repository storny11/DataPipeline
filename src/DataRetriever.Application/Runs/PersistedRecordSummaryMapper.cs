using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Reporting;

namespace DataRetriever.Application.Runs;

public sealed class PersistedRecordSummaryMapper
{
    public IReadOnlyList<PersistedRecordSummary> Map(Step4Output? output)
    {
        return output?.PersistedRecords
            .Select(record => new PersistedRecordSummary(
                record.InternalId,
                record.ExternalId1,
                record.ExternalId2,
                record.Amount1,
                record.Amount2,
                record.Amount3))
            .ToList() ?? [];
    }
}
