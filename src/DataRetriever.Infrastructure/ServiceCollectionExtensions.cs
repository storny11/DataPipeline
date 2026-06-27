// Registers real infrastructure adapters, health checks, and email reporting.
using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step2Load;
using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step4Persist;
using DataRetriever.Infrastructure.Reporting;
using DataRetriever.Infrastructure.Step1Load;
using DataRetriever.Infrastructure.Step2Load;
using DataRetriever.Infrastructure.Step3Load;
using DataRetriever.Infrastructure.Step4Persist;
using DataRetriever.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverInfrastructure(this IServiceCollection services)
    {
        return services.AddDataRetrieverInfrastructure(new ConfigurationBuilder().Build());
    }

    public static IServiceCollection AddDataRetrieverInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDataRetrieverEmailReporting(configuration);

        services.AddOptions<Step2SourceClientOptions>();
        services.AddOptions<Step3SourceClientOptions>();

        services.AddScoped<IStep1SourceClient, Step1SourceClient>();
        services.AddScoped<IStep2SourceClient, Step2SourceClient>();
        services.AddScoped<Step3ExternalClient>();
        services.AddScoped<IStep3SourceClient, Step3SourceClient>();
        services.AddScoped<IStep4SinkClient, Step4SinkClient>();

        services.AddHealthChecks()
            .AddCheck<Step1SourceHealthCheck>("step1-source")
            .AddCheck<Step2SourceHealthCheck>("step2-source")
            .AddCheck<Step3SourceHealthCheck>("step3-source")
            .AddCheck<Step4SinkHealthCheck>("step4-sink");

        return services;
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
