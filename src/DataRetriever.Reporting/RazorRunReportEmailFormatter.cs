// Renders structured run reports into email bodies using Razor templates.
using System.Text;
using DataRetriever.Execution;
using DataRetriever.Reporting.EmailTemplates;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataRetriever.Reporting;

public sealed class RazorRunReportEmailFormatter(
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    IOptions<RunReportEmailOptions> options) : IRunReportEmailFormatter
{
    public async Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);
        var parameters = ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                ["Report"] = report,
                ["DisplayStats"] = options.Value.DisplayStats
            });

        var htmlBody = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<RunReportEmailTemplate>(parameters);
            return component.ToHtmlString();
        });

        cancellationToken.ThrowIfCancellationRequested();

        return new RunReportEmail(
            BuildSubject(report),
            htmlBody,
            BuildTextBody(report, options.Value.DisplayStats));
    }

    private static string BuildSubject(RunReport report)
    {
        return $"Data retrieval {report.Status}: {TableRowCount(report)} table rows, {report.WarningCount} warnings, {report.ErrorCount} errors";
    }

    private static string BuildTextBody(RunReport report, bool displayStats)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Data retrieval run {report.RunId}");
        builder.AppendLine($"Status: {report.Status}");
        builder.AppendLine($"Started: {report.StartedAt:O}");
        builder.AppendLine($"Completed: {report.CompletedAt:O}");

        if (displayStats)
        {
            foreach (var metric in report.Summary)
            {
                builder.AppendLine($"{metric.Label}: {metric.Value}");
            }

            builder.AppendLine($"Warnings: {report.WarningCount}");
            builder.AppendLine($"Errors: {report.ErrorCount}");
        }
        builder.AppendLine();

        AppendIssues(builder, "Errors", report.Issues.Where(issue => issue.Severity == StepIssueSeverity.Error));
        AppendIssues(builder, "Warnings", report.Issues.Where(issue => issue.Severity == StepIssueSeverity.Warning));

        builder.AppendLine("Report tables");
        if (report.Tables.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var table in report.Tables)
            {
                AppendTable(builder, table);
            }
        }

        return builder.ToString();
    }

    private static void AppendTable(StringBuilder builder, RunReportTable table)
    {
        builder.AppendLine();
        builder.AppendLine(table.Title);

        if (table.Rows.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var row in table.Rows)
        {
            var values = table.Columns
                .Select(column => $"{column.Header}={Value(row, column.Key)}");
            builder.AppendLine($"- {string.Join("; ", values)}");
        }
    }

    private static void AppendIssues(StringBuilder builder, string title, IEnumerable<RunReportIssue> issues)
    {
        var issueList = issues.ToList();
        if (issueList.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(title);

        foreach (var issue in issueList)
        {
            builder.AppendLine(
                $"- Step={issue.StepName}; Context={FormatContext(issue.Context)}; Message={issue.Message}");
        }
    }

    private static string FormatContext(DiagnosticContext context)
    {
        return context.Values.Count == 0
            ? "-"
            : string.Join(", ", context.Values.Select(value => $"{value.Key}={value.Value}"));
    }

    private static string Value(IReadOnlyDictionary<string, string?> row, string key)
    {
        return row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "-";
    }

    private static int TableRowCount(RunReport report)
    {
        return report.Tables.Sum(table => table.Rows.Count);
    }
}
