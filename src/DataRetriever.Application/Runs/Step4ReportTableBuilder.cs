// Maps persisted Step 4 output into the service-specific report table.
using System.Globalization;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Reporting;

namespace DataRetriever.Application.Runs;

public sealed class Step4ReportTableBuilder
{
    public RunReportTable Build(Step4Output? output)
    {
        return new RunReportTable(
            "persisted-records",
            "Persisted Records",
            [
                new RunReportColumn("internalId", "Internal Id"),
                new RunReportColumn("externalId1", "External Id 1"),
                new RunReportColumn("externalId2", "External Id 2"),
                new RunReportColumn("amount1", "Amount 1", RunReportColumnAlignment.Right),
                new RunReportColumn("amount2", "Amount 2", RunReportColumnAlignment.Right),
                new RunReportColumn("amount3", "Amount 3", RunReportColumnAlignment.Right)
            ],
            output?.PersistedRecords
                .Select(record => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
                {
                    ["internalId"] = record.InternalId,
                    ["externalId1"] = record.ExternalId1,
                    ["externalId2"] = record.ExternalId2,
                    ["amount1"] = record.Amount1.ToString("N4", CultureInfo.InvariantCulture),
                    ["amount2"] = record.Amount2.ToString("N4", CultureInfo.InvariantCulture),
                    ["amount3"] = record.Amount3.ToString("N4", CultureInfo.InvariantCulture)
                })
                .ToList() ?? []);
    }
}
