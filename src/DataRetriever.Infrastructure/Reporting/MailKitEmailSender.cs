using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DataRetriever.Infrastructure.Reporting;

public sealed class MailKitEmailSender(
    IOptions<EmailRunReportOptions> options,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var emailOptions = options.Value;
        var mimeMessage = CreateMessage(message);

        using var client = new SmtpClient();
        client.Timeout = (int)emailOptions.Timeout.TotalMilliseconds;

        await client.ConnectAsync(
            emailOptions.Host,
            emailOptions.Port,
            emailOptions.SocketOptions,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(emailOptions.UserName))
        {
            await client.AuthenticateAsync(
                emailOptions.UserName,
                emailOptions.Password ?? string.Empty,
                cancellationToken);
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation(
            "Sent report email to {RecipientCount} recipients using SMTP host {Host}",
            message.To.Count + message.Cc.Count + message.Bcc.Count,
            emailOptions.Host);
    }

    private static MimeMessage CreateMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(MailboxAddress.Parse(message.From));
        AddRecipients(mimeMessage.To, message.To);
        AddRecipients(mimeMessage.Cc, message.Cc);
        AddRecipients(mimeMessage.Bcc, message.Bcc);
        mimeMessage.Subject = message.Subject;

        mimeMessage.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        return mimeMessage;
    }

    private static void AddRecipients(InternetAddressList target, IEnumerable<string> recipients)
    {
        foreach (var recipient in recipients.Where(recipient => !string.IsNullOrWhiteSpace(recipient)))
        {
            target.Add(MailboxAddress.Parse(recipient));
        }
    }
}
