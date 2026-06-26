using System.Text;
using DataRetriever.Execution;
using DataRetriever.Reporting.EmailTemplates;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace DataRetriever.Reporting;

public sealed class RazorRunReportEmailFormatter(
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory) : IRunReportEmailFormatter
{
    public async Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);
        var parameters = ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["Report"] = report });

        var htmlBody = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<RunReportEmailTemplate>(parameters);
            return component.ToHtmlString();
        });

        cancellationToken.ThrowIfCancellationRequested();

        return new RunReportEmail(
            BuildSubject(report),
            htmlBody,
            BuildTextBody(report));
    }

    private static string BuildSubject(RunReport report)
    {
        return $"Data retrieval {report.Status}: {report.PersistedRecords.Count} persisted, {report.Summary.WarningCount} warnings, {report.Summary.ErrorCount} errors";
    }

    private static string BuildTextBody(RunReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Data retrieval run {report.RunId}");
        builder.AppendLine($"Status: {report.Status}");
        builder.AppendLine($"Started: {report.StartedAt:O}");
        builder.AppendLine($"Completed: {report.CompletedAt:O}");
        builder.AppendLine($"Configured rows: {report.Summary.ConfiguredRowsReturned}");
        builder.AppendLine($"Rows after filtering: {report.Summary.RowsAfterFiltering}");
        builder.AppendLine($"Step 2 rows: {report.Summary.Step2RowsProduced}");
        builder.AppendLine($"Step 3 valid rows: {report.Summary.ValidStep3RowsReturned}");
        builder.AppendLine($"Rows persisted: {report.Summary.RowsPersisted}");
        builder.AppendLine($"Warnings: {report.Summary.WarningCount}");
        builder.AppendLine($"Errors: {report.Summary.ErrorCount}");
        builder.AppendLine();

        builder.AppendLine("Persisted records");
        if (report.PersistedRecords.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var record in report.PersistedRecords)
            {
                builder.AppendLine(
                    $"- InternalId={record.InternalId}; ExternalId1={record.ExternalId1}; ExternalId2={record.ExternalId2}; Amount1={record.Amount1}; Amount2={record.Amount2}; Amount3={record.Amount3}");
            }
        }

        AppendIssues(builder, "Warnings", report.Issues.Where(issue => issue.Severity == StepIssueSeverity.Warning));
        AppendIssues(builder, "Errors", report.Issues.Where(issue => issue.Severity == StepIssueSeverity.Error));

        return builder.ToString();
    }

    private static void AppendIssues(StringBuilder builder, string title, IEnumerable<RunReportIssue> issues)
    {
        builder.AppendLine();
        builder.AppendLine(title);

        var found = false;
        foreach (var issue in issues)
        {
            found = true;
            builder.AppendLine(
                $"- Step={issue.StepName}; Context={FormatContext(issue.Context)}; Message={issue.Message}");
        }

        if (!found)
        {
            builder.AppendLine("- none");
        }
    }

    private static string FormatContext(DiagnosticContext context)
    {
        return context.Values.Count == 0
            ? "-"
            : string.Join(", ", context.Values.Select(value => $"{value.Key}={value.Value}"));
    }
}
