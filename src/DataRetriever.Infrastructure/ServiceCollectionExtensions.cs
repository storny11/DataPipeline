// Keeps unfinished real adapter registration blocked while exposing real email reporting.
using DataRetriever.Infrastructure.Reporting;
using DataRetriever.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverInfrastructure(this IServiceCollection services)
    {
        return services.AddDataRetrieverInfrastructure(null);
    }

    public static IServiceCollection AddDataRetrieverInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        throw new NotSupportedException(
            "AddDataRetrieverInfrastructure is not available until the real source and sink adapters are implemented. Use simulator adapters, or call AddDataRetrieverEmailReporting for email-only infrastructure.");
    }

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
