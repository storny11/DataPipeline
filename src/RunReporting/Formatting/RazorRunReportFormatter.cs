// Default formatter: renders the run report through a Razor component template.
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

    public async Task<RunReportEmail> FormatAsync(RunReport report, CancellationToken cancellationToken)
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
            var component = await renderer.RenderComponentAsync(_templateType, parameters);
            return component.ToHtmlString();
        });

        cancellationToken.ThrowIfCancellationRequested();

        return new RunReportEmail(BuildSubject(report), htmlBody);
    }

    private string BuildSubject(RunReport report)
    {
        var outcome = report.Outcome switch
        {
            RunOutcome.Failed => $"run failed ({report.ErrorCount} errors, {report.WarningCount} warnings)",
            RunOutcome.CompletedWithWarnings => $"run completed with {report.WarningCount} warnings",
            _ => "run succeeded"
        };

        return $"{options.SubjectPrefix} {options.ApplicationName} {outcome}";
    }
}
