// Defines the infrastructure boundary for sending email messages.
namespace DataRetriever.Infrastructure.Reporting;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
