// Default formatter: renders the run report through Razor component templates.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using RunReporting.Templates;

namespace RunReporting;

public sealed class RazorRunReportFormatter(
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory,
    RunReportingOptions options,
    RunReportTemplate? template = null) : IRunReportFormatter
{
    private readonly Type _templateType = template?.ComponentType ?? typeof(RunReportEmailTemplate);
    private readonly ILogger _logger = loggerFactory.CreateLogger<RazorRunReportFormatter>();

    public Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken)
    {
        return FormatAsync(report, _templateType, cancellationToken);
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
                ["Report"] = report,
                ["Options"] = options
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

        var outcome = report.Outcome switch
        {
            RunOutcome.Failed => $"run failed ({report.ErrorCount} errors, {report.WarningCount} warnings)",
            RunOutcome.CompletedWithWarnings => $"run completed with {report.WarningCount} warnings",
            _ => "run succeeded"
        };

        return SanitizeSubject($"{options.SubjectPrefix} {options.ApplicationName} {outcome}");
    }

    // MailMessage.Subject throws on CR/LF; a multi-line value costs its line breaks, never the email.
    private static string SanitizeSubject(string subject)
    {
        return subject.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
