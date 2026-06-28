// Verifies Dapper-style report table creation from typed or anonymous row objects.
using DataRetriever.Reporting;

namespace DataRetriever.Tests.Reporting;

public sealed class RunReportTableTests
{
    [Fact]
    public void FromRows_UsesObjectShapeForColumnsAndRows()
    {
        var table = RunReportTable.FromRows(
            "persisted-records",
            "Persisted Records",
            new[]
            {
                new
                {
                    InternalId = "INT-1",
                    ExternalId1 = "EXT-A",
                    Amount1 = 10.1m,
                    EffectiveDate = new DateOnly(2026, 6, 28)
                }
            });

        Assert.Equal(["internalId", "externalId1", "amount1", "effectiveDate"], table.Columns.Select(column => column.Key));
        Assert.Equal(["Internal Id", "External Id 1", "Amount 1", "Effective Date"], table.Columns.Select(column => column.Header));
        Assert.Equal(RunReportColumnAlignment.Right, table.Columns.Single(column => column.Key == "amount1").Alignment);

        var row = Assert.Single(table.Rows);
        Assert.Equal("INT-1", row["internalId"]);
        Assert.Equal("EXT-A", row["externalId1"]);
        Assert.Equal("10.1000", row["amount1"]);
        Assert.Equal("2026-06-28", row["effectiveDate"]);
    }

    [Fact]
    public void FromRows_WithNoRows_StillBuildsColumnsFromRowType()
    {
        var table = RunReportTable.FromRows(
            "empty",
            "Empty",
            Array.Empty<ReportRow>());

        Assert.Equal(["internalId", "amount1"], table.Columns.Select(column => column.Key));
        Assert.Empty(table.Rows);
    }

    private sealed record ReportRow(
        string InternalId,
        decimal Amount1);
}
