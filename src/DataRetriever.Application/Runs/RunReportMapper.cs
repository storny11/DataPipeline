// Maps collected RunReporting package models into the API report models.
using DataRetriever.Execution;
using DataRetriever.Reporting;
using RunReporting;

namespace DataRetriever.Application.Runs;

public static class RunReportMapper
{
    public static RunReportIssue ToRunReportIssue(RunIssue issue)
    {
        return new RunReportIssue(
            string.IsNullOrEmpty(issue.StepName) ? "General" : issue.StepName,
            issue.Severity == IssueSeverity.Error ? StepIssueSeverity.Error : StepIssueSeverity.Warning,
            issue.Message,
            new DiagnosticContext(issue.Data));
    }

    public static RunReportTable ToRunReportTable(ResultTable table)
    {
        var columns = table.Fields
            .Select(field => new RunReportColumn(field, field))
            .ToList();

        var rows = table.Rows
            .Select(row =>
            {
                // Indexer assignment tolerates duplicate field names (last value wins) where ToDictionary would throw.
                var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < table.Fields.Count; index++)
                {
                    cells[table.Fields[index]] = row[index];
                }

                return (IReadOnlyDictionary<string, string?>)cells;
            })
            .ToList();

        return new RunReportTable(
            table.Title.ToLowerInvariant().Replace(' ', '-'),
            table.Title,
            columns,
            rows);
    }
}
