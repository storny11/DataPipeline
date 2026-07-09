// Default formatter: renders the run report through Razor component templates.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using RunReporting.Templates;

namespace RunReporting;

public sealed class RazorRunReportFormatter(
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    RunReportingOptions options) : IRunReportFormatter
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<RazorRunReportFormatter>();

    public Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken)
    {
        return FormatAsync(report, typeof(RunReportEmailTemplate), cancellationToken);
    }

    public Task<RunReportEmail> FormatCompactAsync(RunReport report, CancellationToken cancellationToken)
    {
        return FormatAsync(report, typeof(CompactRunReportEmailTemplate), cancellationToken);
    }

    private async Task<RunReportEmail> FormatAsync(RunReport report, Type templateType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);
        var parameters = ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(RunReportEmailTemplate.Report)] = report,
                [nameof(RunReportEmailTemplate.Options)] = options
            });

        var htmlBody = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync(templateType, parameters);
            return component.ToHtmlString();
        });

        cancellationToken.ThrowIfCancellationRequested();

        return new RunReportEmail(BuildSubject(report), htmlBody);
    }

    private string BuildSubject(RunReport report)
    {
        if (options.SubjectBuilder != null)
        {
            try
            {
                var custom = options.SubjectBuilder(report);
                if (!string.IsNullOrWhiteSpace(custom))
                {
                    return SanitizeSubject(custom);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Custom subject builder failed; using the default subject.");
            }
        }

        var phrase = report.Outcome switch
        {
            RunOutcome.Failed => $"Run failed ({report.ErrorCount} errors, {report.WarningCount} warnings)",
            RunOutcome.CompletedWithWarnings => $"Run completed with {report.WarningCount} warnings",
            _ => "Run succeeded"
        };

        var serviceName = options.ServiceName?.Trim();
        var subject = string.IsNullOrWhiteSpace(serviceName)
            ? phrase
            : $"[{serviceName}] {phrase}";

        return SanitizeSubject(subject);
    }

    // MailMessage.Subject throws on CR/LF; a multi-line value costs its line breaks, never the email.
    private static string SanitizeSubject(string subject)
    {
        return subject.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
