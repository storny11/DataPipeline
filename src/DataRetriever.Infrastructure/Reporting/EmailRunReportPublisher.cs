using DataRetriever.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataRetriever.Infrastructure.Reporting;

public sealed class EmailRunReportPublisher(
    IOptions<EmailRunReportOptions> options,
    IRunReportEmailFormatter formatter,
    IEmailSender emailSender,
    ILogger<EmailRunReportPublisher> logger) : IRunReportPublisher
{
    public async Task PublishAsync(RunReport report, CancellationToken cancellationToken)
    {
        var emailOptions = options.Value;
        if (!emailOptions.Enabled)
        {
            logger.LogInformation(
                "Email publishing is disabled. Built report for run {RunId} with {IssueCount} issues and {TableRowCount} table rows.",
                report.RunId,
                report.Issues.Count,
                report.Tables.Sum(table => table.Rows.Count));
            return;
        }

        var configurationErrors = Validate(emailOptions);
        if (configurationErrors.Count > 0)
        {
            logger.LogError(
                "Email publishing is enabled but report email cannot be sent for run {RunId}: {ConfigurationErrors}",
                report.RunId,
                string.Join("; ", configurationErrors));
            return;
        }

        try
        {
            var email = await formatter.FormatAsync(report, cancellationToken);
            var message = new EmailMessage(
                emailOptions.From,
                emailOptions.To,
                emailOptions.Cc,
                emailOptions.Bcc,
                email.Subject,
                email.HtmlBody,
                email.TextBody);

            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to send report email for run {RunId}", report.RunId);
        }
    }

    private static IReadOnlyList<string> Validate(EmailRunReportOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            errors.Add($"{EmailRunReportOptions.SectionName}:Host is required");
        }

        if (options.Port <= 0)
        {
            errors.Add($"{EmailRunReportOptions.SectionName}:Port must be greater than zero");
        }

        if (string.IsNullOrWhiteSpace(options.From))
        {
            errors.Add($"{EmailRunReportOptions.SectionName}:From is required");
        }

        if (options.To.Length == 0 && options.Cc.Length == 0 && options.Bcc.Length == 0)
        {
            errors.Add($"At least one {EmailRunReportOptions.SectionName}:To/Cc/Bcc recipient is required");
        }

        return errors;
    }
}
