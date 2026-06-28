// Registers infrastructure services that are actually available in the template.
using DataRetriever.Infrastructure.Reporting;
using DataRetriever.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverEmailReporting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailRunReportOptions>(configuration.GetSection(EmailRunReportOptions.SectionName));
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddSingleton<IRunReportPublisher, EmailRunReportPublisher>();
        return services;
    }
}
