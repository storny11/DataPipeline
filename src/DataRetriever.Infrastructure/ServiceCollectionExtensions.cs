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
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<Step2SourceClientOptions>();
        services.AddOptions<Step3SourceClientOptions>();
        services.AddOptions<EmailRunReportOptions>();

        services.AddScoped<IStep1SourceClient, Step1SourceClient>();
        services.AddScoped<IStep2SourceClient, Step2SourceClient>();
        services.AddScoped<Step3ExternalClient>();
        services.AddScoped<IStep3SourceClient, Step3SourceClient>();
        services.AddScoped<IStep4SinkClient, Step4SinkClient>();
        services.AddSingleton<IRunReportPublisher, EmailRunReportPublisher>();

        services.AddHealthChecks()
            .AddCheck<Step1SourceHealthCheck>("step1-source")
            .AddCheck<Step2SourceHealthCheck>("step2-source")
            .AddCheck<Step3SourceHealthCheck>("step3-source")
            .AddCheck<Step4SinkHealthCheck>("step4-sink");

        return services;
    }
}
