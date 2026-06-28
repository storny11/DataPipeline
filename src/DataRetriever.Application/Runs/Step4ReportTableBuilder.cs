// Maps persisted Step 4 output into the service-specific report table.
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Reporting;

namespace DataRetriever.Application.Runs;

public sealed class Step4ReportTableBuilder
{
    public RunReportTable Build(Step4Output? output)
    {
        var records = output?.PersistedRecords ?? [];

        return RunReportTable.FromRows(
            "persisted-records",
            "Persisted Records",
            records.Select(record => new
            {
                record.InternalId,
                record.ExternalId1,
                record.ExternalId2,
                record.Amount1,
                record.Amount2,
                record.Amount3
            }));
    }
}
