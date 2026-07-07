// Built-in publisher: formats the report and delivers it over SMTP (works with smtp4dev).
// Options.Enabled turns just this publisher off, leaving any others registered active.
using System.Net;
using System.Net.Mail;

namespace RunReporting;

public sealed class EmailRunReportPublisher(
    RunReportingOptions options,
    IRunReportFormatter formatter) : IRunReportPublisher
{
    public async Task PublishAsync(RunReport report, CancellationToken cancellationToken)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (!options.Enabled)
        {
            return;
        }

        var to = options.ToRecipients;
        var cc = options.CcRecipients;
        var bcc = options.BccRecipients;
        if (to.Count == 0 && cc.Count == 0 && bcc.Count == 0)
        {
            throw new InvalidOperationException(
                "RunReportingOptions must contain at least one To/Cc/Bcc recipient before a report can be published.");
        }

        var email = await formatter.FormatAsync(report, cancellationToken);

        using var message = new MailMessage
        {
            From = new MailAddress(options.From),
            Subject = email.Subject,
            Body = email.HtmlBody,
            IsBodyHtml = true
        };

        AddRecipients(message.To, to);
        AddRecipients(message.CC, cc);
        AddRecipients(message.Bcc, bcc);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseSsl
        };

        if (!string.IsNullOrEmpty(options.UserName))
        {
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        }

        // SmtpClient.Timeout does not apply to SendMailAsync, so bound the send ourselves.
        using var sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sendTimeout.CancelAfter(options.SendTimeout);
        await client.SendMailAsync(message, sendTimeout.Token).ConfigureAwait(false);
    }

    private static void AddRecipients(MailAddressCollection collection, IReadOnlyList<string> recipients)
    {
        foreach (var recipient in recipients)
        {
            collection.Add(recipient);
        }
    }
}
